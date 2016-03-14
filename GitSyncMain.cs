//
// Each repository should have 1 sync file at the root directory of the git repo.
// <repo>\.gitsync
//
// Example .gitsync file
// ---------------------------------------------------------
// SourceRepo ceos
// Sync MyPath/MyFile.ext DestPath
// Sync MyPath/MyFile.ext DestPath/MyFileNewName.ext
// Sync "My Path With Spaces/My File.ext" Dest
// ---------------------------------------------------------
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using More;

namespace More.GitSync
{
    public class GitSyncOptions : CLParser
    {
        public CLStringArgument sourceRepo;
        public CLSwitch help;
        public CLStringArgument path;

        public GitSyncOptions()
        {
            this.sourceRepo = new CLStringArgument('s', "source", "Name or path to source repository");
            Add(this.sourceRepo);

            this.help = new CLSwitch('h', "help", "Display the usage help");
            Add(this.help);

            this.path = new CLStringArgument('C', "path to git repo");
            Add(this.path);
        }

        public override void PrintUsageHeader()
        {
            Console.WriteLine("usage: gitsync [options] <command>");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("   status   Show the sync status");
            Console.WriteLine();
        }
    }


    public static class GitSyncGlobals
    {
        public static String RepoRoot;
        public static String RepoRootParent;

        public static Dictionary<String, String> RepoNameMap;

        public static Byte[] BufferForShaFiles;
        public static Sha1Builder ShaBuilder;

        public static void Init(String repoRoot)
        {
            GitSyncGlobals.RepoRoot = repoRoot;
            try
            {
                GitSyncGlobals.RepoRootParent = Path.GetDirectoryName(repoRoot);
            }
            catch (ArgumentException)
            {
                GitSyncGlobals.RepoRootParent = repoRoot;
            }
            GitSyncGlobals.RepoNameMap = new Dictionary<String, String>(StringComparer.Ordinal);
            GitSyncGlobals.BufferForShaFiles = new Byte[512];
            GitSyncGlobals.ShaBuilder = new Sha1Builder();
        }

        public static Sha1 GetFileSha(String filename)
        {
            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (true)
                {
                    Int32 bytesRead = stream.Read(BufferForShaFiles, 0, BufferForShaFiles.Length);
                    if (bytesRead <= 0)
                    {
                        if (bytesRead < 0)
                            throw new InvalidOperationException(String.Format("Failed ot read file (return code is {0})", filename, bytesRead));
                        return ShaBuilder.Finish(true);
                    }
                    ShaBuilder.Add(BufferForShaFiles, 0, bytesRead);
                }
            }
        }
    }

    public class GitSync
    {
        public const String GitSyncFileName = "gitsync";

        public static String SanitizePath(String directory)
        {
            // normalize path separators
            if (Path.DirectorySeparatorChar != '/')
            {
                directory = directory.Replace('/', Path.DirectorySeparatorChar);
            }

            return directory;
        }
        public static String GetRepoPath(String sanitizedRepoRoot, String unsanitizedRelativeRepoPath)
        {
            if (unsanitizedRelativeRepoPath == "/" || unsanitizedRelativeRepoPath == "\\")
            {
                return sanitizedRepoRoot;
            }
            return Path.Combine(sanitizedRepoRoot, SanitizePath(unsanitizedRelativeRepoPath));
        }
        public static Int32 Main(String[] args)
        {
            GitSyncOptions options = new GitSyncOptions();
            var nonOptionArgs = options.Parse(args);

            if (options.help.set || nonOptionArgs.Count == 0)
            {
                options.PrintUsage();
                return 0;
            }

            // find root of git repository
            String startingPath;
            if (options.path.set)
            {
                startingPath = SanitizePath(options.path.ArgValue);
            }
            else
            {
                startingPath = Environment.CurrentDirectory;
            }

            {
                String repoRoot = TryFindRepoRoot(startingPath);
                if (repoRoot == null)
                {
                    Console.WriteLine("fatal: Not a git repository (or any of the parent directoryes): .git");
                    return 1;
                }
                GitSyncGlobals.Init(repoRoot);
            }

            // TODO: what to do about case-sensitive an insensitive paths?
            for (int i = 0; i < nonOptionArgs.Count; i++)
            {
                Int32 equalIndex = nonOptionArgs[i].IndexOf('=');
                if (equalIndex > 0)
                {
                    String mapping = nonOptionArgs[i];
                    nonOptionArgs.RemoveAt(i);
                    i--;
                    GitSyncGlobals.RepoNameMap.Add(mapping.Remove(equalIndex), SanitizePath(mapping.Substring(equalIndex + 1)));
                }
            }

            String command = nonOptionArgs[0];
            if (command.Equals("status", StringComparison.Ordinal))
            {
                var configs = SyncConfig.TryParseGitSync();
                if (configs == null)
                {
                    return 1;
                }

                foreach (var config in configs)
                {
                    if (!Directory.Exists(config.sourceRepoPath))
                    {
                        Console.WriteLine("fatal: SourceRepo {0} does not exist at '{1}'", config.sourceRepoName, config.sourceRepoPath);
                        return 1;
                    }

                    foreach (var syncFile in config.syncFiles)
                    {
                        String srcPath = GetRepoPath(config.sourceRepoPath, GitSync.SanitizePath(syncFile.src));
                        String dstPath = GetRepoPath(GitSyncGlobals.RepoRoot, GitSync.SanitizePath(syncFile.dst));
                        if (Directory.Exists(dstPath))
                        {
                            dstPath = Path.Combine(dstPath, Path.GetFileName(srcPath));
                        }

                        if (!File.Exists(srcPath))
                        {
                            if (!File.Exists(dstPath))
                            {
                                Console.WriteLine("SYNC    : (does not exist in either repo) {0} > {1}", srcPath, dstPath);
                            }
                            else
                            {
                                Console.WriteLine("MODIFIED: (has been removed in the source repo) {0} > {1}", srcPath, dstPath);
                            }
                        }
                        else if (!File.Exists(dstPath))
                        {
                            Console.WriteLine("MODIFIED: (local file does not exist, needs to be copied from source repo) {0} > {1}", srcPath, dstPath);
                        }
                        else
                        {
                            Sha1 srcHash = GitSyncGlobals.GetFileSha(srcPath);
                            Sha1 dstHash = GitSyncGlobals.GetFileSha(dstPath);
                            if (srcHash.Equals(dstHash))
                            {
                                Console.WriteLine("SYNC    : (file hashes match) {0} > {1}", srcPath, dstPath);
                            }
                            else
                            {
                                Console.WriteLine("MODIFIED: (file hashes DO NOT match) {0} > {1}", srcPath, dstPath);
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("gitsync: '{0}' is not a gitsync command.  See 'gitsync --help'.", command);
                Console.WriteLine();
                return 1;
            }

            return 0;
        }

        static String TryFindRepoRoot(String startingPath)
        {
            StringBuilder pathBuilder = new StringBuilder(startingPath,
                startingPath.Length + 5); // 1 for directory separator, 7 for 'gitsync'
            if (startingPath[startingPath.Length - 1] != Path.DirectorySeparatorChar)
            {
                pathBuilder.Append(Path.DirectorySeparatorChar);
            }

            while (true)
            {
                pathBuilder.Append(".git");
                String dotGitDir = pathBuilder.ToString();

                //Console.WriteLine("[DEBUG] checking '{0}'", dotGitDir);
                if (Directory.Exists(dotGitDir))
                {
                    return Path.GetDirectoryName(dotGitDir);
                }

                pathBuilder.Length -= 4; // remove '.git'

                if (pathBuilder[pathBuilder.Length - 1] == Path.DirectorySeparatorChar)
                {
                    pathBuilder.Length--;
                }
                if (pathBuilder.Length == 0)
                {
                    return null;
                }
                for (int i = pathBuilder.Length - 1; ; i--)
                {
                    if (pathBuilder[i] == Path.DirectorySeparatorChar)
                    {
                        pathBuilder.Length = i + 1;
                        break;
                    }
                    if (i == 0)
                    {
                        return null;
                    }
                }
            }
        }
    }


    public class GitSyncParseException : Exception
    {
        public readonly UInt32 lineNumber;
        public GitSyncParseException(UInt32 lineNumber, String message)
            : base(message)
        {
            this.lineNumber = lineNumber;
        }
    }

    public class SyncConfig
    {
        // Tries to parse the 'gitsync' file, returns null on error, prints it's own errors
        public static List<SyncConfig> TryParseGitSync()
        {
            string pathAndGitSyncFileName = Path.Combine(GitSyncGlobals.RepoRoot, GitSync.GitSyncFileName);

            if (!File.Exists(pathAndGitSyncFileName))
            {
                Console.WriteLine("gitsync file '{0}' does not exist", pathAndGitSyncFileName);
                return null;
            }

            try
            {
                using (LfdReader stream = new LfdReader(new StreamReader(new FileStream(pathAndGitSyncFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))))
                {
                    List<SyncConfig> syncConfigList = new List<SyncConfig>();
                    ParseGitSyncFile(syncConfigList, stream);
                    return syncConfigList;
                }
            }
            catch (GitSyncParseException e)
            {
                if (e.lineNumber == 0)
                {
                    Console.WriteLine("Error in .gitsync file: {0}", e.Message);
                }
                else
                {
                    Console.WriteLine("Error in .gitsync file line {0}: {1}", e.lineNumber, e.Message);
                }
                return null;
            }
        }

        // Returns true on success
        public static void ParseGitSyncFile(List<SyncConfig> syncConfigList, LfdReader stream)
        {
            // Read the SourceRepo
            LfdLine line = stream.ReadLine();

            while (true)
            {
                if (line == null)
                {
                    break;
                }

                if (!line.id.Equals("SourceRepo", StringComparison.Ordinal))
                {
                    throw new GitSyncParseException(line.actualLineNumber,
                        String.Format("expected SourceRepo but got {0}", line.id));
                }

                UInt32 argCount = (line.fields == null) ? 0 : (UInt32)line.fields.Length;
                if (argCount != 1)
                {
                    throw new GitSyncParseException(line.actualLineNumber,
                        String.Format("expected SourceRepo directive to have 1 argument but got {0}", argCount));
                }
                String sourceRepo = line.fields[0];

                List<Sync> syncFiles = new List<Sync>();
                ParseSyncCommands(out line, syncFiles, stream);
                syncConfigList.Add(new SyncConfig(sourceRepo, syncFiles));
            }
        }

        // Returns true on success
        static void ParseSyncCommands(out LfdLine line, List<Sync> syncFiles, LfdReader stream)
        {
            while (true)
            {
                line = stream.ReadLine();
                if (line == null)
                {
                    return;
                }

                if (line.id.Equals("Sync", StringComparison.Ordinal))
                {
                    UInt32 argCount = (line.fields == null) ? 0 : (UInt32)line.fields.Length;
                    if (argCount != 2)
                    {
                        throw new GitSyncParseException(line.actualLineNumber,
                            String.Format("expected Sync directive to have 2 arguments but got {0}", argCount));
                    }
                    syncFiles.Add(new Sync(line.fields[0], line.fields[1]));
                }
                else
                {
                    return;
                }
            }
        }

        public struct Sync
        {
            public readonly String src;
            public readonly String dst;

            public Sync(String src, String dst)
            {
                this.src = src;
                this.dst = dst;
            }
        }

        public readonly String sourceRepoName;
        public readonly String sourceRepoPath;
        public readonly List<Sync> syncFiles = new List<Sync>();
        public SyncConfig(String sourceRepoName, List<Sync> syncFiles)
        {
            this.sourceRepoName = sourceRepoName;

            if (!GitSyncGlobals.RepoNameMap.TryGetValue(sourceRepoName, out this.sourceRepoPath))
            {
                this.sourceRepoPath = Path.Combine(GitSyncGlobals.RepoRootParent, sourceRepoName);
            }

            this.syncFiles = syncFiles;
        }
    }

}
