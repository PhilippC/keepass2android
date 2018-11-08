<div class="wikidoc">
<h1>How to use Keepass2Android with YubiKey NEO</h1>
<p>Please refer to the documentation on the Keepass website (<a href="http://keepass.info/help/kb/yubikey.html">http://keepass.info/help/kb/yubikey.html</a>) or the Yubico website (<a href="http://www.yubico.com/applications/password-management/consumer/keepass/">http://www.yubico.com/applications/password-management/consumer/keepass/</a>)
 on how to set up a Keepass 2 database with Yubikey/OTP protection.<br>
<br>
After successful setup you should have the database file, e.g. yubi.kdbx, and the OTP auxiliary file, e.g. yubi.otp.xml, both in the same folder.<br>
<a href="How to use Keepass2Android with YubiKey NEO_OTPAuxFile_2.png"><img title="OTPAuxFile" src="How to use Keepass2Android with YubiKey NEO_OTPAuxFile_thumb.png" alt="OTPAuxFile" width="513" height="40" border="0" style="padding-top:0px; padding-left:0px; display:inline; padding-right:0px; border:0px"></a></p>
<p>Make sure you make <strong>both files</strong> available to Keepass2Android, e.g. by placing them both in your Dropbox.</p>
<p>Now you should check your NDEF setup of the Yubikey NEO. Therefore, go to the Tools menu in the Yubico Personalization Utility. Select the same slot as used for OTPs with Keepass 2. The default setting for NDEF type and payload should work. If you experience
 problems, you may use the configuration as shown in this screenshot or simply press the &ldquo;Reset&rdquo; button:</p>
<p><a href="How to use Keepass2Android with YubiKey NEO_image_2.png"><img title="image" src="How to use Keepass2Android with YubiKey NEO_image_thumb.png" alt="image" width="760" height="622" border="0" style="padding-top:0px; padding-left:0px; display:inline; padding-right:0px; border:0px"></a></p>
<p><br>
<br>
In Keepass2Android, select &quot;Open file&quot; and locate your database file, e.g. yubi.kdbx.<br>
<br>
In the password screen under &quot;Select master key type&quot; select &quot;Password &#43; OTP&quot;.</p>
<p><a href="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-38-50_2.png"><img title="Screenshot_2013-12-13-06-38-50" src="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-38-50_thumb.png" alt="Screenshot_2013-12-13-06-38-50" width="204" height="360" border="0" style="padding-top:0px; padding-left:0px; display:inline; padding-right:0px; border:0px"></a></p>
<p>Click &quot;Load auxiliary OTP file&quot;. This is required to load the information how many OTPs must be entered. As loading the file might require user action in some cases, this is not performed automatically.<br>
<a href="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-38-12_2.png"><img title="Screenshot_2013-12-13-06-38-12" src="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-38-12_thumb.png" alt="Screenshot_2013-12-13-06-38-12" width="204" height="360" border="0" style="padding-top:0px; padding-left:0px; display:inline; padding-right:0px; border:0px"></a><br>
After loading the OTP auxiliary file, you should see a few text fields for entering the OTPs. Now swipe your YubiKey NEO at the back of your Android device. If you have multiple apps which can handle NFC actions, you might be prompted to select which app to
 use. Select Keepass2Android in this case. Swipe your YubiKey again until all OTP fields are filled. Note: You don't need to select the next text field, this is done automatically!<br>
<a href="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-38-36_2.png"><img title="Screenshot_2013-12-13-06-38-36" src="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-38-36_thumb.png" alt="Screenshot_2013-12-13-06-38-36" width="204" height="360" border="0" style="padding-top:0px; padding-left:0px; display:inline; padding-right:0px; border:0px"></a><br>
Don't forget to also enter your password and click OK. You will see the &ldquo;Saving auxiliary OTP file&hellip;&rdquo; dialog. Note that there is some encryption envolved which is probably fast on your PC but might take some time on your mobile device. You
 can reduce the look-ahead window length to speed this up.<br>
<a href="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-39-47_2.png"><img title="Screenshot_2013-12-13-06-39-47" src="How to use Keepass2Android with YubiKey NEO_Screenshot_2013-12-13-06-39-47_thumb.png" alt="Screenshot_2013-12-13-06-39-47" width="204" height="360" border="0" style="padding-top:0px; padding-left:0px; display:inline; padding-right:0px; border:0px"></a></p>
<h2>&nbsp;</h2>
<h2>A note about offline access</h2>
<p>If your database is stored in the cloud or on the web, you can still access it if you have enabled file caching (which is on by default). With OTPs, this becomes a little bit more complicated: If you repeatedly open your datbase while being offline, the
 OTP counter stored on the Yubikey will be increased. Don&rsquo;t forget to synchronize the database (which will also synchronize the OTP auxiliary file) as soon as possible to avoid problems with accessing your database on other devices! If you often need
 to open the database while you&rsquo;re offline, consider increasing the look-ahead window length!</p>
</div><div class="ClearBoth"></div>
