﻿//-----------------------------------------------------------------------
// <copyright file="NuGetPackageHandlerTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Plugins.Test.Common;
using System.IO;
using NuGet;
using System.Linq;
using System.Collections.Generic;

namespace SonarQube.Plugins.Roslyn.RoslynPluginGeneratorTests
{
    /// <summary>
    /// Tests for NuGetPackageHandler.cs
    /// 
    /// There is no test for the scenario released package A depending on prerelease package B as this is not allowed.
    /// </summary>
    [TestClass]
    public class NuGetPackageHandlerTests
    {
        public TestContext TestContext { get; set; }

        private const string TestPackageId = "testPackage";
        private const string DependentPackageId = "dependentPackage";

        private const string ReleaseVersion = "1.0.0";
        private const string PreReleaseVersion = "1.0.0-RC1";

        #region Tests

        [TestMethod]
        public void NuGet_TestPackageDownload_Release_Release()
        {
            // Arrange
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");

            // Create test NuGet payload and packages
            IPackageRepository fakeRemoteRepo = BuildTestPackages(true, true);

            TestLogger logger = new TestLogger();
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, logger);

            // Act
            // Attempt to download a package which is released with a dependency that is released
            IPackage package = handler.FetchPackage(DependentPackageId, null);

            // Assert
            AssertExpectedPackage(package, DependentPackageId, ReleaseVersion);
            // Packages should have been downloaded
            AssertPackageDownloaded(targetNuGetRoot, DependentPackageId, ReleaseVersion);
            AssertPackageDownloaded(targetNuGetRoot, TestPackageId, ReleaseVersion);
        }

        [TestMethod]
        public void NuGet_TestPackageDownload_PreRelease_Release()
        {
            // Arrange
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");

            // Create test NuGet payload and packages
            IPackageRepository fakeRemoteRepo = BuildTestPackages(false, true);

            TestLogger logger = new TestLogger();
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, logger);

            // Act
            // Attempt to download a package which is not released with a dependency that is released
            IPackage package = handler.FetchPackage(DependentPackageId, null);

            // Assert
            AssertExpectedPackage(package, DependentPackageId, PreReleaseVersion);
            // Packages should have been downloaded
            AssertPackageDownloaded(targetNuGetRoot, DependentPackageId, PreReleaseVersion);
            AssertPackageDownloaded(targetNuGetRoot, TestPackageId, ReleaseVersion);
        }

        [TestMethod]
        public void NuGet_TestPackageDownload_PreRelease_PreRelease()
        {
            // Arrange
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");

            // Create test NuGet payload and packages
            IPackageRepository fakeRemoteRepo = BuildTestPackages(false, false);

            TestLogger logger = new TestLogger();
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, logger);

            // Act
            // Attempt to download a package which is not released with a dependency that is not released
            IPackage package = handler.FetchPackage(DependentPackageId, null);

            // Assert
            AssertExpectedPackage(package, DependentPackageId, PreReleaseVersion);
            // Packages should have been downloaded
            AssertPackageDownloaded(targetNuGetRoot, DependentPackageId, PreReleaseVersion);
            AssertPackageDownloaded(targetNuGetRoot, TestPackageId, PreReleaseVersion);
        }

        [TestMethod]
        public void FetchPackage_VersionSpecified_CorrectVersionSelected()
        {
            string fakeRemoteNuGetDir = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.remote");
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");
            IPackageManager mgr = CreatePackageManager(fakeRemoteNuGetDir);

            BuildAndInstallPackage(mgr, "package.id.1", "0.8.0");
            BuildAndInstallPackage(mgr, "package.id.1", "1.0.0-rc1");
            BuildAndInstallPackage(mgr, "package.id.1", "2.0.0");

            BuildAndInstallPackage(mgr, "dummy.package.1", "0.8.0");

            BuildAndInstallPackage(mgr, "package.id.1", "0.9.0");
            BuildAndInstallPackage(mgr, "package.id.1", "1.0.0");

            IPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemoteNuGetDir);
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, new TestLogger());

            // Check for specific versions
            IPackage actual = handler.FetchPackage("package.id.1", new SemanticVersion("0.8.0"));
            AssertExpectedPackage(actual, "package.id.1", "0.8.0");

            actual = handler.FetchPackage("package.id.1", new SemanticVersion("1.0.0-rc1"));
            AssertExpectedPackage(actual, "package.id.1", "1.0.0-rc1");

            actual = handler.FetchPackage("package.id.1", new SemanticVersion("2.0.0"));
            AssertExpectedPackage(actual, "package.id.1", "2.0.0");
        }

        [TestMethod]
        public void FetchPackage_VersionNotSpecified_ReleaseVersionExists_LastReleaseVersionSelected()
        {
            // Arrange
            string fakeRemoteNuGetDir = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.remote");
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");
            IPackageManager mgr = CreatePackageManager(fakeRemoteNuGetDir);

            BuildAndInstallPackage(mgr, "package.id.1", "0.8.0");
            BuildAndInstallPackage(mgr, "package.id.1", "0.9.0-rc1");
            BuildAndInstallPackage(mgr, "package.id.1", "1.0.0");
            BuildAndInstallPackage(mgr, "package.id.1", "1.1.0-rc1");
            BuildAndInstallPackage(mgr, "dummy.package.1", "2.0.0");

            IPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemoteNuGetDir);
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, new TestLogger());

            // Act
            IPackage actual = handler.FetchPackage("package.id.1", null);

            // Assert
            AssertExpectedPackage(actual, "package.id.1", "1.0.0");
        }

        [TestMethod]
        public void FetchPackage_VersionNotSpecified_NoReleaseVersions_LastPreReleaseVersionSelected()
        {
            // Arrange
            string fakeRemoteNuGetDir = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.remote");
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");
            IPackageManager mgr = CreatePackageManager(fakeRemoteNuGetDir);

            BuildAndInstallPackage(mgr, "package.id.1", "0.9.0-rc1");
            BuildAndInstallPackage(mgr, "package.id.1", "1.0.0-rc1");
            BuildAndInstallPackage(mgr, "package.id.1", "1.1.0-rc1");
            BuildAndInstallPackage(mgr, "dummy.package.1", "2.0.0");
            BuildAndInstallPackage(mgr, "dummy.package.1", "2.0.0-rc2");

            IPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemoteNuGetDir);
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, new TestLogger());

            // Act
            IPackage actual = handler.FetchPackage("package.id.1", null);

            // Assert
            AssertExpectedPackage(actual, "package.id.1", "1.1.0-rc1");
        }

        [TestMethod]
        public void FetchPackage_PackageNotFound_NullReturned()
        {
            // Arrange
            string fakeRemoteNuGetDir = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.remote");
            string targetNuGetRoot = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.target");
            IPackageManager mgr = CreatePackageManager(fakeRemoteNuGetDir);

            BuildAndInstallPackage(mgr, "package.id.1", "0.8.0");
            BuildAndInstallPackage(mgr, "package.id.1", "0.9.0");

            IPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemoteNuGetDir);
            NuGetPackageHandler handler = new NuGetPackageHandler(fakeRemoteRepo, targetNuGetRoot, new TestLogger());

            // 1. Package id not found
            IPackage actual = handler.FetchPackage("unknown.package.id", new SemanticVersion("0.8.0"));
            Assert.IsNull(actual, "Not expecting a package to be found");

            // 2. Package id not found
            actual = handler.FetchPackage("package.id.1", new SemanticVersion("0.7.0"));
            Assert.IsNull(actual, "Not expecting a package to be found");
        }

        #endregion

        #region Private methods

        private IPackageManager CreatePackageManager(string rootDir)
        {
            IPackageRepository repo = PackageRepositoryFactory.Default.CreateRepository(rootDir);
            PackageManager mgr = new PackageManager(repo, rootDir);

            return mgr;
        }

        private ManifestMetadata GenerateTestMetadata(bool isReleased)
        {
            return new ManifestMetadata()
            {
                Authors = "Microsoft",
                Version = isReleased ? ReleaseVersion : PreReleaseVersion,
                Id = TestPackageId,
                Description = "A description",
            };
        }

        private ManifestMetadata GenerateTestMetadataWithDependency(bool isReleased, bool isDependencyReleased)
        {
            List<ManifestDependencySet> dependencies = new List<ManifestDependencySet>()
            {
                new ManifestDependencySet()
                {
                    Dependencies = new List<ManifestDependency>()
                    {
                        new ManifestDependency()
                        {
                            Id = TestPackageId,
                            Version = isDependencyReleased ? ReleaseVersion : PreReleaseVersion,
                        }
                    }
                }
            };
            return new ManifestMetadata()
            {
                Authors = "Microsoft",
                Version = isReleased ? ReleaseVersion : PreReleaseVersion,
                Id = DependentPackageId,
                Description = "A description",
                DependencySets = dependencies,
            };
        }

        private Stream BuildPackageToStream(ManifestMetadata metadata, Stream outputStream)
        {
            string testDir = TestUtils.EnsureTestDirectoryExists(this.TestContext, "source");
            string dummyTextFile = TestUtils.CreateTextFile(Guid.NewGuid().ToString(), testDir, "content");

            PackageBuilder packageBuilder = new PackageBuilder();

            PhysicalPackageFile file = new PhysicalPackageFile();
            file.SourcePath = dummyTextFile;
            file.TargetPath = "dummy.txt";
            packageBuilder.Files.Add(file);

            packageBuilder.Populate(metadata);
            packageBuilder.Save(outputStream);

            // Assert correct function of the above code when versions are specifically "Release" or "Prerelease"            
            if (String.Equals(metadata.Version, ReleaseVersion, StringComparison.OrdinalIgnoreCase))
            {
                Assert.IsTrue(packageBuilder.IsReleaseVersion());
            }
            else if (String.Equals(metadata.Version, PreReleaseVersion, StringComparison.OrdinalIgnoreCase))
            {
                Assert.IsFalse(packageBuilder.IsReleaseVersion());
            }

            return outputStream;
        }

        private void BuildAndInstallPackage(IPackageManager manager, string id, string version)
        {
            ManifestMetadata metadata = GenerateTestMetadata(true);
            metadata.Id = id;
            metadata.Version = new SemanticVersion(version).ToString();

            Stream packageStream = BuildPackageToStream(metadata, new MemoryStream());
            packageStream.Position = 0;

            ZipPackage pkg = new ZipPackage(packageStream);
            manager.InstallPackage(pkg, true, true);
        }

        private void BuildAndSavePackage(ManifestMetadata metadata, string fileName)
        {
            string destinationName = fileName + "." + metadata.Version + ".nupkg";
            Stream fileStream = File.Open(destinationName, FileMode.OpenOrCreate);
            BuildPackageToStream(metadata, fileStream);
            fileStream.Dispose();
        }

        private IPackageRepository BuildTestPackages(bool isDependentPackageReleased, bool isTestPackageReleased)
        {
            string fakeRemoteNuGetDir = TestUtils.CreateTestDirectory(this.TestContext, ".nuget.remote");

            string testPackageFile = Path.Combine(fakeRemoteNuGetDir, TestPackageId);
            string dependentPackageFile = Path.Combine(fakeRemoteNuGetDir, DependentPackageId);

            ManifestMetadata testMetadata = GenerateTestMetadata(isTestPackageReleased);
            BuildAndSavePackage(testMetadata, testPackageFile);

            ManifestMetadata dependentMetadata = GenerateTestMetadataWithDependency(isDependentPackageReleased, isTestPackageReleased);
            BuildAndSavePackage(dependentMetadata, dependentPackageFile);

            LocalPackageRepository fakeRemoteRepo = new LocalPackageRepository(fakeRemoteNuGetDir);

            // Sanity check the test setup: check we can retrieve the new packages
            Assert.IsNotNull(fakeRemoteRepo.FindPackage(TestPackageId), "Test setup error: failed to locate test package '{0}'", TestPackageId);
            Assert.IsNotNull(fakeRemoteRepo.FindPackage(DependentPackageId), "Test setup error: failed to locate test package '{0}'", DependentPackageId);

            return fakeRemoteRepo;
        }
        
        #endregion

        #region Checks

        private static void AssertExpectedPackage(IPackage actual, string expectedId, string expectedVersion)
        {
            Assert.IsNotNull(actual, "The package should not be null");

            SemanticVersion sVersion = new SemanticVersion(expectedVersion);

            Assert.AreEqual(expectedId, actual.Id, "Unexpected package id");
            Assert.AreEqual(sVersion, actual.Version, "Unexpected package version");
        }

        private void AssertPackageDownloaded(string downloadDir, string expectedName, string expectedVersion)
        {
            string packageDir = Directory.GetDirectories(downloadDir)
                .SingleOrDefault(d => d.Contains(expectedName) && d.Contains(expectedVersion));
            Assert.IsNotNull(packageDir,
                "Expected a package to have been downloaded: " + expectedName);
        }

        #endregion
    }
}