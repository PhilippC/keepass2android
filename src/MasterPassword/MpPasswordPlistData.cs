using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterPassword
{
	public partial class MpAlgorithm
	{
		private const string plist = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<plist version=""1.0"">
<dict>
	<key>MPElementGeneratedEntity</key>
	<dict>
		<key>Maximum Security Password</key>
		<array>
			<string>anoxxxxxxxxxxxxxxxxx</string>
			<string>axxxxxxxxxxxxxxxxxno</string>
		</array>
		<key>Long Password</key>
		<array>
			<string>CvcvnoCvcvCvcv</string>
			<string>CvcvCvcvnoCvcv</string>
			<string>CvcvCvcvCvcvno</string>
			<string>CvccnoCvcvCvcv</string>
			<string>CvccCvcvnoCvcv</string>
			<string>CvccCvcvCvcvno</string>
			<string>CvcvnoCvccCvcv</string>
			<string>CvcvCvccnoCvcv</string>
			<string>CvcvCvccCvcvno</string>
			<string>CvcvnoCvcvCvcc</string>
			<string>CvcvCvcvnoCvcc</string>
			<string>CvcvCvcvCvccno</string>
			<string>CvccnoCvccCvcv</string>
			<string>CvccCvccnoCvcv</string>
			<string>CvccCvccCvcvno</string>
			<string>CvcvnoCvccCvcc</string>
			<string>CvcvCvccnoCvcc</string>
			<string>CvcvCvccCvccno</string>
			<string>CvccnoCvcvCvcc</string>
			<string>CvccCvcvnoCvcc</string>
			<string>CvccCvcvCvccno</string>
		</array>
		<key>Medium Password</key>
		<array>
			<string>CvcnoCvc</string>
			<string>CvcCvcno</string>
		</array>
		<key>Basic Password</key>
		<array>
			<string>aaanaaan</string>
			<string>aannaaan</string>
			<string>aaannaaa</string>
		</array>
		<key>Short Password</key>
		<array>
			<string>Cvcn</string>
		</array>
		<key>PIN</key>
		<array>
			<string>nnnn</string>
		</array>
	</dict>
	<key>MPCharacterClasses</key>
	<dict>
		<key>V</key>
		<string>AEIOU</string>
		<key>C</key>
		<string>BCDFGHJKLMNPQRSTVWXYZ</string>
		<key>v</key>
		<string>aeiou</string>
		<key>c</key>
		<string>bcdfghjklmnpqrstvwxyz</string>
		<key>A</key>
		<string>AEIOUBCDFGHJKLMNPQRSTVWXYZ</string>
		<key>a</key>
		<string>AEIOUaeiouBCDFGHJKLMNPQRSTVWXYZbcdfghjklmnpqrstvwxyz</string>
		<key>n</key>
		<string>0123456789</string>
		<key>o</key>
		<string>@&amp;%?,=[]_:-+*$#!'^~;()/.</string>
		<key>x</key>
		<string>AEIOUaeiouBCDFGHJKLMNPQRSTVWXYZbcdfghjklmnpqrstvwxyz0123456789!@#$%^&amp;*()</string>
	</dict>
</dict>
</plist>
";
	}
}
