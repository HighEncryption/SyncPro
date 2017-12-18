namespace SyncPro.UnitTests
{
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using SyncPro.Adapters;
    using SyncPro.Runtime;

    [TestClass]
    public class EntryProcessingSorterTests
    {
        [TestMethod]
        public void BasicOrder()
        {
            List<EntryUpdateInfo> input = new List<EntryUpdateInfo>
            {
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.Deleted, @"a\b\c"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.NewDirectory, @"a\b\c\d"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.FileSize, @"a\b\c\e"),
            };

            input.Sort(new EntryProcessingSorter());

            Assert.AreEqual(0, input.FindIndex(e => e.RelativePath == @"a\b\c\d"));
            Assert.AreEqual(1, input.FindIndex(e => e.RelativePath == @"a\b\c\e"));
            Assert.AreEqual(2, input.FindIndex(e => e.RelativePath == @"a\b\c"));
        }

        [TestMethod]
        public void SortyByRelativePath()
        {
            List<EntryUpdateInfo> input = new List<EntryUpdateInfo>
            {
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.NewDirectory, @"a\newDir1"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.NewDirectory, @"a\newDir3"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.NewDirectory, @"a\newDir2"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.FileSize, @"a\newFile2"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.FileSize, @"a\newFile1"),
            };

            input.Sort(new EntryProcessingSorter());

            Assert.AreEqual(0, input.FindIndex(e => e.RelativePath == @"a\newDir1"));
            Assert.AreEqual(1, input.FindIndex(e => e.RelativePath == @"a\newDir2"));
            Assert.AreEqual(2, input.FindIndex(e => e.RelativePath == @"a\newDir3"));
            Assert.AreEqual(3, input.FindIndex(e => e.RelativePath == @"a\newFile1"));
            Assert.AreEqual(4, input.FindIndex(e => e.RelativePath == @"a\newFile2"));
        }

        [TestMethod]
        public void DeletedReverseOrder()
        {
            List<EntryUpdateInfo> input = new List<EntryUpdateInfo>
            {
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.Deleted, @"a\deleted2"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.Deleted, @"a\deleted1"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.Deleted, @"a\deleted3"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.Deleted, @"a\deleted2\child1"),
                EntryUpdateInfo.CreateForTests(SyncEntryChangedFlags.Deleted, @"a\deleted2\child1\two"),
            };

            input.Sort(new EntryProcessingSorter());

            Assert.AreEqual(0, input.FindIndex(e => e.RelativePath == @"a\deleted3"));
            Assert.AreEqual(1, input.FindIndex(e => e.RelativePath == @"a\deleted2\child1\two"));
            Assert.AreEqual(2, input.FindIndex(e => e.RelativePath == @"a\deleted2\child1"));
            Assert.AreEqual(3, input.FindIndex(e => e.RelativePath == @"a\deleted2"));
            Assert.AreEqual(4, input.FindIndex(e => e.RelativePath == @"a\deleted1"));
        }
    }
}