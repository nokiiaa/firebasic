using firebasic.AST;
using NIR;
using NIR.Backends;
using NIR.Instructions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace firebasic
{
    class Program
    {
        static void Main(string[] args)
        {
            var files = new List<string>();
            bool windows = false;

            foreach (string arg in args)
            {
                if (arg == "/win")
                    windows = true;
                else
                    files.Add(arg);
            }

            if (files.Count < 2)
            {
                Console.WriteLine("Usage: firebasic <input files | flags> <output file>");
                Console.WriteLine("Flags: /win (passes -mwindows to linker)");
                Environment.Exit(1);
            }

            string outExe = files.Last();
            files.RemoveAt(files.Count - 1);

            var objFiles = new List<string>();

            objFiles.Add(X64Backend.GenerateEntryStub("Main"));

            foreach (string file in files)
            {
                try
                {
                    string shortFile = Path.GetFileName(file);
                    var tokens = Lexer.Scan(shortFile, File.ReadAllText(file));
                    var parser = new Parser(shortFile, tokens);
                    var unit = parser.Parse();
                    if (Output.Errors > 0) goto end;
                    var generated = new IRGenerator(unit).Translate();
                    var cg = new CodeGenerator(generated, new X64ArchitectureInfo());
                    objFiles.Add(cg.Generate());
                }
                catch (IOException)
                {
                    Output.Error(file, 0, 0, "Failed to read file");
                    Environment.Exit(1);
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "gcc",
                Arguments = $"{string.Join(" ", objFiles)} -s -eWinMain{(windows ? " -mwindows" : "")} -o {outExe}",
                WorkingDirectory = Environment.CurrentDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }).WaitForExit();

            foreach (string obj in objFiles)
                File.Delete(obj);

            end:;
        }
    }
}