/*
    fsclean - Cleans up files on the filesystem.
    Copyright (C) 2009 Brodrick E. Bassham, Jr.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace fsclean
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = null;
            bool deleteEmptyDir = false;
            TimeSpan maxAgeToKeep = TimeSpan.MaxValue;
            List<Regex> filesToIgnore = new List<Regex>();
            string logPath = null;
            bool quiet = false;

            foreach (string arg in args)
            {
                if (arg.StartsWith("-p:", StringComparison.InvariantCultureIgnoreCase))
                {
                    path = arg.Substring(3);
                }
                else if (string.Compare(arg, "--delete-empty", StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    deleteEmptyDir = true;
                }
                else if (arg.StartsWith("-a:", StringComparison.InvariantCultureIgnoreCase))
                {
                    maxAgeToKeep = GetTimeSpanFromArg(arg.Substring(3));
                }
                else if (arg.StartsWith("--path:", StringComparison.InvariantCultureIgnoreCase))
                {
                    path = arg.Substring(7);
                }
                else if (arg.StartsWith("--max-age:", StringComparison.InvariantCultureIgnoreCase))
                {
                    maxAgeToKeep = GetTimeSpanFromArg(arg.Substring(10));
                }
                else if (arg.StartsWith("-i:", StringComparison.InvariantCultureIgnoreCase))
                {
                    filesToIgnore.Add(new Regex(arg.Substring(3), RegexOptions.Compiled));
                }
                else if (arg.StartsWith("--ignore:", StringComparison.InvariantCultureIgnoreCase))
                {
                    filesToIgnore.Add(new Regex(arg.Substring(9), RegexOptions.Compiled));
                }
                else if (arg.StartsWith("-l:", StringComparison.InvariantCultureIgnoreCase))
                {
                    logPath = arg.Substring(3);
                }
                else if (arg == "-q" || arg == "--quiet")
                {
                    quiet = true;
                }
            }

            if (!quiet)
            {
                Console.WriteLine("fsclean - Cleans up files on the filesystem.");
                Console.WriteLine("Copyright (C) 2009 Brodrick E. Bassham, Jr.");
                Console.WriteLine();
            }

            if (!string.IsNullOrEmpty(logPath))
            {
                TextWriterTraceListener l = new TextWriterTraceListener(Path.Combine(logPath, string.Format("{0:yyMMdd}.log", DateTime.Now)));

                Trace.Listeners.Add(l);
            }

            if (string.IsNullOrEmpty(path) || maxAgeToKeep == TimeSpan.MaxValue)
            {
                PrintUsage();
                return;
            }

            RecursiveDelete(path, deleteEmptyDir, maxAgeToKeep, filesToIgnore);

            Trace.Flush();
            Trace.Close();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: fsclean.exe -p:<path> -a:<maxAgeToKeep> [-l:<logPath>] [-i:<ignoreRegex>] [--quiet|-q]");
        }

        private static TimeSpan GetTimeSpanFromArg(string arg)
        {
            try
            {
                if (arg.EndsWith("d", StringComparison.InvariantCultureIgnoreCase))
                {
                    return TimeSpan.FromDays(double.Parse(arg.Substring(0, arg.Length - 1)));
                }
                else if (arg.EndsWith("days", StringComparison.InvariantCultureIgnoreCase))
                {
                    return TimeSpan.FromDays(double.Parse(arg.Substring(0, arg.Length - 4)));
                }
                else if (arg.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
                {
                    return TimeSpan.FromHours(double.Parse(arg.Substring(0, arg.Length - 1)));
                }
                else if (arg.EndsWith("hours", StringComparison.InvariantCultureIgnoreCase))
                {
                    return TimeSpan.FromHours(double.Parse(arg.Substring(0, arg.Length - 5)));
                }
                else if (arg.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
                {
                    return TimeSpan.FromMinutes(double.Parse(arg.Substring(0, arg.Length - 1)));
                }
                else if (arg.EndsWith("minutes", StringComparison.InvariantCultureIgnoreCase))
                {
                    return TimeSpan.FromMinutes(double.Parse(arg.Substring(0, arg.Length - 7)));
                }
                else
                {
                    return TimeSpan.Parse(arg);
                }
            }
            catch (Exception)
            {
                return TimeSpan.MaxValue;
            }
        }

        private static void RecursiveDelete(string path, bool deleteEmptyDir, TimeSpan maxAgeToKeep, List<Regex> filesToIgnore)
        {
            foreach (string dir in Directory.EnumerateDirectories(path))
            {
                RecursiveDelete(dir, deleteEmptyDir, maxAgeToKeep, filesToIgnore);
            }

            foreach (string file in Directory.EnumerateFiles(path))
            {
                if ((File.GetLastWriteTime(file) + maxAgeToKeep) < DateTime.Now)
                {
                    bool ignore = false;

                    foreach (Regex r in filesToIgnore)
                    {
                        if (r.IsMatch(file))
                        {
                            ignore = true;
                            break;
                        }
                    }

                    if (!ignore)
                    {
                        DeleteFile(file);
                    }
                }
            }

            if (deleteEmptyDir)
            {
                string[] remaining = Directory.GetFileSystemEntries(path);

                if (remaining.Length == 0)
                {
                    bool ignore = false;

                    foreach (Regex r in filesToIgnore)
                    {
                        if (r.IsMatch(path))
                        {
                            ignore = true;
                            break;
                        }
                    }

                    if (!ignore)
                    {
                        DeleteDirectory(path);
                    }
                }
            }
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                Trace.Write(string.Format("Deleting {0}...", path));
                Directory.Delete(path);
                Trace.WriteLine("succeeded.");
            }
            catch
            {
                // ignore error and keep going
                Trace.WriteLine("failed.");
            }
        }

        private static void DeleteFile(string path)
        {
            try
            {
                Trace.Write(string.Format("Deleting {0}...", path));
                File.Delete(path);
                Trace.WriteLine("succeeded.");
            }
            catch
            {
                try
                {
                    FileAttributes fa = File.GetAttributes(path);

                    if ((fa & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(path, fa & ~FileAttributes.ReadOnly);
                        File.Delete(path);
                        Trace.WriteLine("succeeded.");
                    }
                }
                catch
                {
                    // ignore error and keep going
                    Trace.WriteLine("failed.");
                }
            }
        }
    }
}
