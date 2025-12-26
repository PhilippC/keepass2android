using NUnit.Framework;
using keepass2android.KeeShare;
using System;
using System.Text;
using KeePassLib;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace KeeShare.Tests
{
    [TestFixture]
    public class KeeShareSettingsTests
    {
        [Test]
        public void TestParseReference()
        {
            var group = new PwGroup();

            // Create a valid XML for reference
            var xml = @"<KeeShare>
                          <Type><Import/><Export/></Type>
                          <Group>MTIzNDU2Nzg5MDEyMzQ1Ng==</Group>
                          <Path>c2hhcmVkLmtiZHg=</Path>
                          <Password>cGFzc3dvcmQ=</Password>
                          <KeepGroups>True</KeepGroups>
                        </KeeShare>";

            // "shared.kbdx" in base64
            // "password" in base64
            // 1234567890123456 in base64

            group.CustomData.Set(KeeShareSettings.KeeShareReferenceKey,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)));

            var refObj = KeeShareSettings.GetReference(group);

            Assert.That(refObj, Is.Not.Null);
            Assert.That((refObj.Type & KeeShareSettings.TypeFlag.ImportFrom) != 0, Is.True);
            Assert.That((refObj.Type & KeeShareSettings.TypeFlag.ExportTo) != 0, Is.True);
            Assert.That(refObj.Path, Is.EqualTo("shared.kbdx"));
            Assert.That(refObj.Password, Is.EqualTo("password"));
            Assert.That(refObj.KeepGroups, Is.True);
        }

        [Test]
        public void TestDeviceSpecificPath_Override()
        {
            var group = new PwGroup();
            
            // Base reference
            // Path: "shared.kbdx" (c2hhcmVkLmtiZHg=)
            var xml = @"<KeeShare>
                          <Path>c2hhcmVkLmtiZHg=</Path>
                        </KeeShare>"; 

            group.CustomData.Set(KeeShareSettings.KeeShareReferenceKey,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)));

            // Set device specific override
            string deviceId = "TestDevice123";
            string devicePath = "device_specific.kdbx";
            
            group.CustomData.Set("KeeShare.FilePath." + deviceId, devicePath);

            // Mock the provider
            var originalProvider = KeeShareSettings.DeviceIdProvider;
            try 
            {
                KeeShareSettings.DeviceIdProvider = () => deviceId;

                var refObj = KeeShareSettings.GetReference(group);
                Assert.That(refObj, Is.Not.Null);
                Assert.That(refObj.Path, Is.EqualTo(devicePath));
            }
            finally
            {
                KeeShareSettings.DeviceIdProvider = originalProvider;
            }
        }

        [Test]
        public void TestDeviceSpecificPath_NoMatch()
        {
            var group = new PwGroup();
            
            // Base reference
            // Path: "shared.kbdx" (c2hhcmVkLmtiZHg=)
            var xml = @"<KeeShare>
                          <Path>c2hhcmVkLmtiZHg=</Path>
                        </KeeShare>"; 

            group.CustomData.Set(KeeShareSettings.KeeShareReferenceKey,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(xml)));

            // Set device specific override for OTHER device
            string otherDeviceId = "OtherDevice";
            string devicePath = "device_specific.kdbx";
            group.CustomData.Set("KeeShare.FilePath." + otherDeviceId, devicePath);

            // Mock the provider
            var originalProvider = KeeShareSettings.DeviceIdProvider;
            try 
            {
                KeeShareSettings.DeviceIdProvider = () => "MyCurrentDevice";

                var refObj = KeeShareSettings.GetReference(group);
                Assert.That(refObj, Is.Not.Null);
                Assert.That(refObj.Path, Is.EqualTo("shared.kbdx")); // Should use base path
            }
            finally
            {
                KeeShareSettings.DeviceIdProvider = originalProvider;
            }
        }
    }
}
