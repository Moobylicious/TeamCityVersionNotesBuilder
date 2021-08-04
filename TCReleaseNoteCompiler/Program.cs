using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCReleaseNoteCompiler.Build;

namespace TCReleaseNoteCompiler
{
    class Program
    {
        static string[] IgnoredCommitPrefixes = new[] { "wip:", "@wip" };

        static int Main(string[] args)
        {
            //arg1 = TC base url.
            var tcBase = args[0];
            //arg 2 - username
            var user = args[1];

            //arg 3 - password
            var password = args[2];

            //arg4 = project ID
            var projectId = args[3];

            //arg 5 - output file name
            var outputfile = args[4];

            
            bool getMergeCommits = false;
            bool getFileChangeCommitDetails = false;
            bool getJustComments = true;
            //arg 4 - if we want merge commits, includes "merges"
            //if we want file commits, includes 'files' and changset nos.
            //if absent, assume just getting comments for simple list of changes - no changeset/commits, just commit messages.
            if (args.Length > 5)
            {
                getJustComments = false;
                //get only 'merge' commits.  Usually good for releases, but relies on the 'merge' commit 
                getMergeCommits = args[5].ToLower().Contains("merges");
                getFileChangeCommitDetails = args[5].ToLower().Contains("full");
            }

            var buildsUrl = $"app/rest/builds?locator=buildType(id:{projectId})";


            //http://localhost:32768/app/rest/changes?locator=buildType:(id:CashDesk_Release)
            var changesUrl = $"app/rest/changes?locator=buildType:(id:{projectId}),start:0,count:1000";

            var client = new RestClient(tcBase)
            {
                Authenticator = new HttpBasicAuthenticator(user, password)
            };


            var dtConverter = new CustomDateTimeConverter();
            var request = new RestRequest(buildsUrl);

            var result = client.Execute(request);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            { 
                return -1;
            }


            request = new RestRequest(buildsUrl);
            

            try
            {

                result = client.Execute(request);

                if (result != null && result.Content != null && result.StatusCode == System.Net.HttpStatusCode.OK)
                {

                    BuildsResponse builds = JsonConvert.DeserializeObject<BuildsResponse>(result.Content, dtConverter);

                    //Changes changes = JsonConvert.DeserializeObject<Changes>(result.Content);

                    var simpleChanges = new StringBuilder();

                    if ((builds?.count ?? 0) > 0)
                    {
                        foreach (var bld in builds.build/*.Where(b => b.status == "SUCCESS")*/.OrderByDescending(b => b.id))
                        {
                            try
                            {

                                request = new RestRequest(bld.href);

                                result = client.Execute(request);

                                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                                {
                                    return -1;
                                }

                                var buildinfo = JsonConvert.DeserializeObject<Build.Build>(result.Content, dtConverter);

                                if (string.IsNullOrEmpty(buildinfo?.changes?.href))
                                {
                                    return -1;
                                }

                                request = new RestRequest(buildinfo.changes.href);
                                result = client.Execute(request);

                                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                                {
                                    return -1;
                                }
                                var changes = JsonConvert.DeserializeObject<Changes>(result.Content, dtConverter);
                                var thisChangeList = new StringBuilder();
                                if (changes?.change?.Any() ?? false)
                                {

                                    //iterate over all changes and build list of them
                                    foreach (var c in changes.change)
                                    {
                                        try
                                        {
                                            if (!string.IsNullOrEmpty(c.href))
                                            {
                                                var detailRequest = new RestRequest(c.href);
                                                var detailResponse = client.Execute(detailRequest);
                                                if (detailResponse.IsSuccessful)
                                                {
                                                    var thisChangeDetail = JsonConvert.DeserializeObject<ChangeDetail>(detailResponse.Content, dtConverter);
                                                    if (thisChangeDetail != null)
                                                    {
                                                        var addComment = false;
                                                        //only get changes which changed a file (merge changes just repeat the same comments)
                                                        if (getFileChangeCommitDetails && (thisChangeDetail.files?.file?.Any() ?? false))
                                                        {
                                                            var fileList = string.Join("\r\n    ", thisChangeDetail.files?.file?.Select(f => f.file));

                                                            if (getFileChangeCommitDetails)
                                                            {
                                                                thisChangeList.AppendLine($"### {thisChangeDetail.id} - {thisChangeDetail.date}");

                                                                if (!string.IsNullOrEmpty(fileList))
                                                                    thisChangeList.AppendLine($"```\r\n    {fileList}\r\n```");
                                                            }

                                                            addComment = true;

                                                        }
                                                        else if (getMergeCommits 
                                                            && !(thisChangeDetail.files?.file?.Any() ?? false)
                                                            && (thisChangeDetail.comment.ToLower().StartsWith("merge")))
                                                        {
                                                            var commentLines = thisChangeDetail.comment.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                                            
                                                            if ((commentLines.Count() <= 1) && (thisChangeDetail.comment.ToLower().StartsWith("merge branch")))
                                                            {
                                                                //a single line with a comment starting 'merge branch' indicates a branch merge only commit, which we don't
                                                                //need to bother including in release notes.
                                                                addComment = false;
                                                            } else if (
                                                                (commentLines.Count() > 1) 
                                                                && (commentLines.Last().Trim().StartsWith("* ")) 
                                                                && (commentLines.Last().Trim().EndsWith(":"))
                                                                )
                                                            {
                                                                //this appears to be another comment like:
                                                                //
                                                                //Merge branch 'release' into develop
                                                                //* release:
                                                                //
                                                                //...which we don't need?
                                                                addComment = false;
                                                            }
                                                            else
                                                            {
                                                                //remove unnecessary lines re. merges.
                                                                if (commentLines.First().ToLower().StartsWith("merge "))
                                                                {
                                                                    commentLines = commentLines.Skip(1).ToList();
                                                                }
                                                                //if only one word on first line and ends with colon, remove that (branch label)
                                                                if (commentLines.First().StartsWith("* ") && commentLines.First().EndsWith(":"))
                                                                {
                                                                    commentLines = commentLines.Skip(1).ToList();
                                                                }

                                                                addComment = true;
                                                            }

                                                            if (addComment)
                                                            {
                                                                thisChangeList.AppendLine($"## {thisChangeDetail.date}");

                                                                //Change comment to make it more markdown-friendly.
                                                                thisChangeDetail.comment = 
                                                                    string.Join("\r\n", commentLines
                                                                    .Select(l =>
                                                                     {
                                                                         var thisLine = l.Trim();
                                                                         if (!thisLine.StartsWith("*"))
                                                                         {
                                                                             thisLine = $"* {thisLine}";
                                                                         }
                                                                         return thisLine;
                                                                     })
                                                                 );
                                                            }
                                                        }
                                                        else if (getJustComments && !thisChangeDetail.comment.ToLower().StartsWith("merge "))
                                                        {
                                                            //If not getting files or merge commits, we just get all the comments and append them...
                                                            addComment = true;
                                                        }

                                                        
                                                        if (addComment)
                                                        {
                                                            var commentLines = thisChangeDetail.comment.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                                                            var finalCommentLines = new List<string>();
                                                            //Any comment lines which start with [! and end with ]?
                                                            //highlight those specially!
                                                            foreach (var cl in commentLines)
                                                            {
                                                                var line = cl.Trim();
                                                                //check for replacement bits with <i> tags for rendering to website.
                                                                //only do this if NOT doing 'full' mode
                                                                if (!getFileChangeCommitDetails)
                                                                {
                                                                    var warningStart = line.IndexOf("[!");
                                                                    var warningEnd = line.IndexOf("!]", warningStart >= 0 ? warningStart : 0);
                                                                    if (warningStart >= 0 || warningEnd > warningStart)
                                                                    {
                                                                        var firstPart = line.Substring(0, warningStart);
                                                                        var warningContents = line.Substring(warningStart + 2, warningEnd - (warningStart + 2));
                                                                        var lastPart = line.Substring(warningEnd + 2, line.Length - (warningEnd + 2));

                                                                        //render to <i> tags....
                                                                        line = $"{firstPart}<i class='special warning-triangle'>{warningContents}</i>{lastPart}";
                                                                    }
                                                                }
                                                                //don't add lines which start with WIP: or @wip, unless building FULL notes...
                                                                if (getFileChangeCommitDetails || (!IgnoredCommitPrefixes.Any(x=>line.Trim().ToLower().StartsWith(x))))
                                                                //if (!line.Trim().ToLower().StartsWith("wip:"))
                                                                {
                                                                    finalCommentLines.Add(line);
                                                                }
                                                            }
                                                            
                                                            //Change comment to make it more markdown-friendly.
                                                            thisChangeDetail.comment =
                                                                string.Join("\r\n", finalCommentLines
                                                                .Select(l =>
                                                                {
                                                                    var thisLine = l.Trim();
                                                                    if (!thisLine.StartsWith("*"))
                                                                    {
                                                                        thisLine = $"* {thisLine}";
                                                                    }
                                                                    return thisLine;
                                                                })
                                                             );

                                                            if (!string.IsNullOrEmpty(thisChangeDetail.comment))
                                                            {
                                                                thisChangeList.AppendLine(thisChangeDetail.comment);
                                                            }
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            thisChangeList.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                            thisChangeList.AppendLine($"Error attempting to compile details for change Id {c.id} via {c.href}");
                                            thisChangeList.AppendLine(ex.Message);
                                            thisChangeList.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                            Console.WriteLine($"Failed to deserialize response for change Id {c.id} via {c.href}");
                                            Console.WriteLine(ex.Message);
                                        }
                                    }
                                    if (thisChangeList.Length > 0)
                                    {
                                        //format from the API call is a bit odd, and has no separator info.
                                        //so take first 4, next 2, next 2, etc...
                                        string hackedDate = "n/a";
                                        try
                                        {
                                            hackedDate =
                                                $"{buildinfo.finishDate.Substring(0, 4)}/{buildinfo.finishDate.Substring(4, 2)}/{buildinfo.finishDate.Substring(6, 2)} " +
                                                $"{buildinfo.finishDate.Substring(9, 2)}:{buildinfo.finishDate.Substring(11, 2)}:{buildinfo.finishDate.Substring(13, 2)}";

                                        }
                                        catch
                                        {

                                        }

                                        //DateTime.TryParse(buildinfo.finishDate, out buildDate);
                                        //only append build info if there are some changes...
                                        simpleChanges.AppendLine($"# Build {bld.number} (id:{bld.id}) Build Date:{hackedDate}");
                                        simpleChanges.AppendLine(thisChangeList.ToString());
                                        simpleChanges.AppendLine("--------------");
                                    }
                                }
                                else
                                {
                                    string hackedDate = "n/a";
                                    try
                                    {
                                        hackedDate =
                                            $"{buildinfo.finishDate.Substring(0, 4)}/{buildinfo.finishDate.Substring(4, 2)}/{buildinfo.finishDate.Substring(6, 2)} " +
                                            $"{buildinfo.finishDate.Substring(9, 2)}:{buildinfo.finishDate.Substring(11, 2)}:{buildinfo.finishDate.Substring(13, 2)}";

                                    }
                                    catch
                                    {

                                    }

                                    //DateTime.TryParse(buildinfo.finishDate, out buildDate);
                                    //only append build info if there are some changes...
                                    simpleChanges.AppendLine($"# Build {bld.number} (id:{bld.id}) Build Date:{hackedDate}");
                                    simpleChanges.AppendLine("No Changes");
                                    simpleChanges.AppendLine("--------------");
                                }
                            }
                            catch (Exception ex)
                            {
                                simpleChanges.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                simpleChanges.AppendLine($"Error attempting to compile details for BUILD Id {bld.id} via {bld.href}");
                                simpleChanges.AppendLine(ex.Message);
                                simpleChanges.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                Console.WriteLine($"Failed to deserialize response for change Id {bld.id} via {bld.href}");
                                Console.WriteLine(ex.Message);
                            }
                        }
                        System.IO.File.WriteAllText($"{outputfile}", simpleChanges.ToString());
                    }

                    return 0;
                }
                else
                {
                    return (int)result.StatusCode;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception raised!");
                var innerEx = ex;
                while (innerEx != null)
                {
                    Console.WriteLine(innerEx.Message);
                    innerEx = innerEx.InnerException;
                }
                return -1;
            }

        }
    }


}
