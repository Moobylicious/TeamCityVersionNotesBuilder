using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Linq;
using System.Text;
using TCReleaseNoteCompiler.Build;

namespace TCReleaseNoteCompiler
{
    class Program
    {
        static int Main(string[] args)
        {
            //arg1 = TC base url.
            var tcBase = args[0];
            //arg2 = project ID
            var projectId = args[1];

            //arg 3 - output file name
            var outputfile = args[2];


            var buildsUrl = $"app/rest/builds?locator=buildType(id:{projectId})";



            //http://localhost:32768/app/rest/changes?locator=buildType:(id:CashDesk_Release)
            var changesUrl = $"app/rest/changes?locator=buildType:(id:{projectId}),start:0,count:1000";

            var client = new RestClient(tcBase)
            {
                Authenticator = new HttpBasicAuthenticator("rest", "rest")
            };


            var dtConverter = new CustomDateTimeConverter();
            var request = new RestRequest(buildsUrl);

            var result = client.Execute(request);
            if (result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                //Get builds.
            }
            else
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
                        foreach (var bld in builds.build.Where(b => b.status == "SUCCESS").OrderByDescending(b => b.id))
                        {
                            try
                            {
                                simpleChanges.AppendLine($"# Build {bld.number} (id:{bld.id})");

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
                                                        //only get changes which changed a file (merge changes just repeat the same comments)
                                                        if (thisChangeDetail.files?.file?.Any() ?? false)
                                                        {
                                                            var fileList = string.Join(";  ", thisChangeDetail.files?.file?.Select(f => f.file));

                                                            simpleChanges.AppendLine($"## {thisChangeDetail.id} - {thisChangeDetail.date}");
                                                            simpleChanges.AppendLine(thisChangeDetail.comment);


                                                            if (!string.IsNullOrEmpty(fileList))
                                                                simpleChanges.AppendLine($"    [{fileList}]");

                                                            simpleChanges.AppendLine();
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            simpleChanges.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                            simpleChanges.AppendLine($"Error attempting to compile details for change Id {c.id} via {c.href}");
                                            simpleChanges.AppendLine(ex.Message);
                                            simpleChanges.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                                            Console.WriteLine($"Failed to deserialize response for change Id {c.id} via {c.href}");
                                            Console.WriteLine(ex.Message);
                                        }
                                    }
                                }
                                simpleChanges.AppendLine("--------------");
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
