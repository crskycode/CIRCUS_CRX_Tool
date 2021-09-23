using System;
using System.IO;

namespace CIRCUS_CRX
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("CIRCUS CRX Tool");
                Console.WriteLine("  -- Created by Crsky");
                Console.WriteLine("Usage:");
                Console.WriteLine("  Export   : CrxTool -e [image.crx|folder]");
                Console.WriteLine("  Build    : CrxTool -b [image.json|folder]");
                Console.WriteLine();
                Console.WriteLine("Help:");
                Console.WriteLine("  This tool is only works with CRXG files,");
                Console.WriteLine("    please check the file header first.");
                Console.WriteLine("  Metadata (.json) and image (.png) are required to build CRX.");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            string mode = args[0];
            string path = Path.GetFullPath(args[1]);

            switch (mode)
            {
                case "-e":
                {
                    void Export(string filePath)
                    {
                        Console.WriteLine($"Exporting data from {Path.GetFileName(filePath)}");

                        try
                        {
                            var image = new CRXG();
                            image.Load(filePath);
                            image.ExportMetadata(Path.ChangeExtension(filePath, "json"));
                            image.ExportAsPng(Path.ChangeExtension(filePath, "png"));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (Utility.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*.crx"))
                        {
                            Export(item);
                        }
                    }
                    else
                    {
                        Export(path);
                    }

                    break;
                }
                case "-b":
                {
                    void Build(string filePath)
                    {
                        try
                        {
                            string pngFilePath = Path.ChangeExtension(filePath, "png");
                            string crxFilePath = Path.ChangeExtension(filePath, "crx");

                            Console.WriteLine($"Building {Path.GetFileName(crxFilePath)}");

                            var image = new CRXG();
                            image.ImportMetadata(filePath);
                            image.ImportFromPng(pngFilePath);
                            image.Save(crxFilePath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (Utility.PathIsFolder(path))
                    {
                        foreach (var item in Directory.EnumerateFiles(path, "*.json"))
                        {
                            Build(item);
                        }
                    }
                    else
                    {
                        Build(path);
                    }

                    break;
                }
            }
        }
    }
}
