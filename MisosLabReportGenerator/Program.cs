using CommandLine;
using Microsoft.Build.Construction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MisosLabReportGenerator
{
    internal class Options
    {
        [Value(0)]
        public string Path { get; set; }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
              .WithParsed(RunOptions)
              .WithNotParsed(HandleParseError);
            Console.ReadKey();
        }

        static void RunOptions(Options options)
        {
            if (File.Exists(options.Path) && Path.GetExtension(options.Path) == ".sln")
            {
                GenerateReportForSolution(options.Path);
            }
            else if (Directory.Exists(options.Path) && File.Exists(Path.Combine(options.Path, "Program.cs")))
            {
                GenerateReportForProject(options.Path);
            }
        }

        static void GenerateReportForSolution(string solutionFile)
        {
            string solutionName = Path.GetFileNameWithoutExtension(solutionFile);
            Console.WriteLine($"solution {solutionName}");

            var solution = SolutionFile.Parse(solutionFile);

            Directory.CreateDirectory("result");
            string solutionReportDir = Path.Combine("result", Path.GetFileNameWithoutExtension(solutionFile));
            Directory.CreateDirectory(solutionReportDir);
            Console.WriteLine($"solutionReportDir: {solutionReportDir}");
            foreach (var project in solution.ProjectsInOrder)
            {
                GenerateReportForProject(solutionReportDir, Path.GetDirectoryName(project.AbsolutePath));
            }

            Console.WriteLine($"solution {solutionName} ok");
        }

        static void GenerateReportForProject(string projectDir)
        {
            Directory.CreateDirectory("result");
            string solutionReportDir = Path.Combine("result", "Default");
            Directory.CreateDirectory(solutionReportDir);
            GenerateReportForProject(solutionReportDir, projectDir);
        }

        static void GenerateReportForProject(string solutionReportDir, string projectDir)
        {
            string projectName = Path.GetFileName(projectDir);
            Console.WriteLine($"project {projectName}");

            Directory.CreateDirectory(solutionReportDir);
            string projectReportDir = Path.Combine(solutionReportDir, projectName);
            Directory.CreateDirectory(projectReportDir);
            Console.WriteLine($"projectReportDir: {projectReportDir}");

            string programFilePathCs = Path.Combine(projectDir, "Program.cs");
            string programFilePathCpp = Path.Combine(projectReportDir, "temp.cpp");
            string programFileCodeCs = File.ReadAllText(programFilePathCs);
            string programFileCodeCpp = CsToCpp(programFileCodeCs);
            File.WriteAllText(programFilePathCpp, programFileCodeCpp);

            string graphFilePathDot = Path.Combine(projectReportDir, "graph.dot");
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.WorkingDirectory = AppContext.BaseDirectory;
            process.StartInfo.Arguments = $"/c cxx2flow.exe \"{programFilePathCpp}\" -o \"{graphFilePathDot}\"";
            process.Start();
            process.WaitForExit();

            File.Delete(programFilePathCpp);

            File.WriteAllText(graphFilePathDot, GraphPostProcessing(File.ReadAllText(graphFilePathDot)));

            Console.WriteLine($"graphFile: {graphFilePathDot}");

            string graphFilePathPng = Path.Combine(projectReportDir, "graph.png");
            process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.WorkingDirectory = AppContext.BaseDirectory;
            process.StartInfo.Arguments = $"/c graphviz\\dot.exe \"{graphFilePathDot}\" -Tpng -o {graphFilePathPng}";
            process.Start();
            process.WaitForExit();
            Console.WriteLine($"imageFile: {graphFilePathPng}");

            Console.WriteLine($"project {projectName} ok");
        }

        static string GraphPostProcessing(string graph)
        {
            return graph
                .Replace("label=\"begin\"", "label=\"начало\"")
                .Replace("label=\"end\"", "label=\"конец\"")
                .Replace("xlabel=N", "xlabel=\"нет\"")
                .Replace("xlabel=Y", "xlabel=\"да\"");
        }

        static string CsToCpp(string cs)
        {
            int mainIndex = cs.IndexOf("static void Main");
            if (mainIndex == -1)
            {
                return $"int main(){{{cs}}}";
            }
            else
            {
                int firstBracketIndex = cs.IndexOf("{", mainIndex);
                int lastBracketIndex = firstBracketIndex + 1;
                for (int c = 1; c != 0; lastBracketIndex++)
                {
                    if (cs[lastBracketIndex] == '{')
                    {
                        c++;
                    }
                    else if (cs[lastBracketIndex] == '}')
                    {
                        c--;
                    }
                }
                return "int main()" + cs.Substring(firstBracketIndex, lastBracketIndex - firstBracketIndex);
            }
        }

        static void HandleParseError(IEnumerable<Error> errors)
        {
            //handle errors
        }
    }
}
