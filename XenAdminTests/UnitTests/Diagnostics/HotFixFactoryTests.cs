/* Copyright (c) Citrix Systems, Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using Moq;
using NUnit.Framework;
using XenAdmin.Diagnostics.Hotfixing;
using XenAPI;

namespace XenAdminTests.UnitTests.Diagnostics
{
    [TestFixture, Category(TestCategories.SmokeTest)]
    public class HotFixFactoryTests : UnitTester_TestFixture
    {
        private const string id = "test";

        public HotFixFactoryTests() : base(id){}

        private readonly HotfixFactory factory = new HotfixFactory();

        [TearDown]
        public void TearDownPerTest()
        {
            ObjectManager.ClearXenObjects(id);
            ObjectManager.RefreshCache(id);
        }

        [Test]
        public void HotfixableServerVersionHasExpectedMembers()
        {
            string[] enumNames = Enum.GetNames(typeof (HotfixFactory.HotfixableServerVersion));
            Array.Sort(enumNames);

            string[] expectedNames = new []{"Clearwater", "Creedence", "Dundee", "ElyJura"};
            Array.Sort(expectedNames);

            CollectionAssert.AreEqual(expectedNames, enumNames, "Expected contents of HotfixableServerVersion enum");
        }

        [Test]
        public void UUIDLookedUpFromEnum()
        {
            Assert.AreEqual("591d0209-531e-4ed8-9ed2-98df2a1a445c", 
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.Clearwater).UUID,
                            "Clearwater UUID lookup from enum");

            Assert.AreEqual("3f92b111-0a90-4ec6-b85a-737f241a3fc1 ",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.Creedence).UUID,
                            "Creedence UUID lookup from enum");

            Assert.AreEqual("f6014211-7611-47ac-ac4c-e66bb1692c35",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.Dundee).UUID,
                            "Dundee UUID lookup from enum");

            Assert.AreEqual("ddd68553-2bf8-411d-99bc-ed4a95265840",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.ElyJura).UUID,
                            "Ely-Jura UUID lookup from enum");
        }

        [Test]
        public void FilenameLookedUpFromEnum()
        {
            Assert.AreEqual("RPU001",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.Clearwater).Filename,
                            "Clearwater Filename lookup from enum");

            Assert.AreEqual("RPU002",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.Creedence).Filename,
                            "Creedence Filename lookup from enum");

            Assert.AreEqual("RPU003",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.Dundee).Filename,
                            "Dundee Filename lookup from enum");

            Assert.AreEqual("RPU004",
                            factory.Hotfix(HotfixFactory.HotfixableServerVersion.ElyJura).Filename,
                            "Ely-Jura Filename lookup from enum");
        }

        [Test]
        [TestCase("2.5.50", Description = "Kolkata")]
        [TestCase("9999.9999.9999", Description = "Future")]
        public void TestPlatformVersionNumbersInvernessOrGreaterGiveNulls(string platformVersion)
        {
            Mock<Host> host = ObjectManager.NewXenObject<Host>(id);
            host.Setup(h => h.PlatformVersion()).Returns(platformVersion);
            Assert.IsNull(factory.Hotfix(host.Object));
        }

        [Test]
        [TestCase("2.5.50", Description = "Kolkata", ExpectedResult = false)]
        [TestCase("2.5.0", Description = "Jura", ExpectedResult = true)]
        [TestCase("2.4.0", Description = "Inverness", ExpectedResult = true)]
        [TestCase("2.3.0", Description = "Falcon", ExpectedResult = true)]
        [TestCase("2.1.1", Description = "Ely", ExpectedResult = true)]
        [TestCase("2.0.0", Description = "Dundee", ExpectedResult = true)]
        [TestCase("1.9.0", Description = "Creedence", ExpectedResult = true)]
        [TestCase("1.8.0", Description = "Clearwater", ExpectedResult = true)]
        [TestCase("9999.9999.9999", Description = "Future", ExpectedResult = false)]
        public bool TestIsHotfixRequiredBasedOnPlatformVersion(string version)
        {
            Mock<Host> host = ObjectManager.NewXenObject<Host>(id);
            host.Setup(h => h.PlatformVersion()).Returns(version);
            return factory.IsHotfixRequired(host.Object);
        }
    }
}