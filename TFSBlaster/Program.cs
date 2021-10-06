using System;
using TFSBlasterLib;

namespace TFSBlaster
{
    class Program
    {
        private static BlasterMaster master;

        static void Main(string[] args)
        {
            var argsParseResult = new ArgsParseResult().Parse(args);
            if (argsParseResult.PrintMan)
            {
                PrintMan();
                End();
                return;
            }
            if (argsParseResult.Failed)
            {
                Console.WriteLine("Invalid arguments.");
                Console.WriteLine(argsParseResult.FailReason);
                PrintMan();
                End();
                return;
            }
            else
            {
                try
                {
                    master = new BlasterMaster();
                    master.SendBlastFromJSON(argsParseResult.BlastJsonPath,
                                         argsParseResult.CredentialsJsonPath, 
                                         true); //, argsParseResult.TimeoutMillis);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Terminated with error: {ex.ToString()}");
                    End();
                    return;
                }
                Console.WriteLine("That blast is in the past...");
                End();
            }
        }

        static void PrintMan()
        {
            Console.WriteLine("TFSBlaster by Robert Elser");
            Console.WriteLine();
            Console.WriteLine("Usage: TFSBlaster [relative path to blast JSON] [relative path to credentials JSON]");
            Console.WriteLine();
            Console.WriteLine("Credentials JSON format:");
            Console.WriteLine(master.GetCredentialsJsonSchema());
            Console.WriteLine();
            Console.WriteLine("Blast JSON format:");
            Console.WriteLine(master.GetBlastJsonSchema());
        }

        static void End()
        {
            Console.WriteLine("[Press any key to exit]");
            Console.ReadKey();
        }
    }

    class ArgsParseResult
    {
        public bool Failed;
        public string FailReason;
        public bool PrintMan;
        public Uri BlastJsonPath;
        public Uri CredentialsJsonPath;
        public int? TimeoutMillis;

        public ArgsParseResult Parse(string[] args)
        {
            if (args.Length > 0 && args[0] == "?")
            {
                PrintMan = true;
                return this;
            }

            bool argsCountIsRight = (args.Length >= 2);
            if (!argsCountIsRight)
            {
                Failed = true;
                FailReason = $"Incorrect arg count: {args.Length}";
                return this;
            }

            try
            {
                BlastJsonPath = new Uri(args[0], UriKind.Relative);
                CredentialsJsonPath = new Uri(args[1], UriKind.Relative);
                if (args.Length > 2)
                    TimeoutMillis = int.Parse(args[2]);
            }
            catch (Exception ex)
            {
                Failed = true;
                FailReason = ex.ToString();
                return this;
            }

            return this;
        }
    }
}
