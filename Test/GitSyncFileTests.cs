using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using More;

namespace More.GitSync
{
    [TestClass]
    public class GitSyncFileTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            GitSyncGlobals.Init("");
        }

        void TestInvalidFileContent(UInt32 errorLineNumber, String content)
        {
            List<SyncConfig> configs = new List<SyncConfig>();

            using(LfdReader reader = new LfdReader(new StringReader(content)))
            {
                try
                {
                    SyncConfig.ParseGitSyncFile(configs, reader);
                    Assert.Fail("Expected exception but did not get one for:\r\n{0}", content);
                }
                catch (GitSyncParseException e)
                {
                    Assert.AreEqual(errorLineNumber, e.lineNumber);
                    Console.WriteLine("Got expected exception: {0}", e.Message);
                }
            }
        }
        void TestValidFileContent(String content)
        {
            List<SyncConfig> configs = new List<SyncConfig>();

            using (LfdReader reader = new LfdReader(new StringReader(content)))
            {
                SyncConfig.ParseGitSyncFile(configs, reader);
            }
        }

        [TestMethod]
        public void TestInvalidGitSyncFiles()
        {
            TestInvalidFileContent(1, "SourceRepo");
            TestValidFileContent("SourceRepo myrepo");
            TestInvalidFileContent(1, "SourceRepo myrepo extra-arg");

            TestValidFileContent("SourceRepo a\r\nSourceRepo b\r\n");
            TestInvalidFileContent(2, "SourceRepo a\r\nSourceRepo\r\n");
        }
    }
}
