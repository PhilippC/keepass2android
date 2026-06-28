// This file is part of Keepass2Android, Copyright 2025 Philipp Crocoll.
//
//   Keepass2Android is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   Keepass2Android is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with Keepass2Android.  If not, see <http://www.gnu.org/licenses/>.

using System;
using keepass2android.Io;

namespace Kp2aS3PathCodec.Tests
{
  public class S3PathCodecTests
  {
    // ---- settings round-trip (the encode/decode that worried the reviewer) ----

    [Theory]
    // plain
    [InlineData("AKIAEXAMPLE", "simplesecret", S3Provider.Aws, "us-east-1", "")]
    // a real-shaped AWS secret: base64 contains '+' and '/' and may end with '='
    [InlineData("AKIA/IDX+1", "wJalrXUtnFEMI/K7MDENG+bPxRfiCYz+a/b=", S3Provider.Aws, "eu-west-3", "")]
    // values containing the path delimiters ':' '#' '/' must survive
    [InlineData("ak:with#weird/chars", "sk:with#weird/chars+and space", S3Provider.Custom, "us-east-1", "https://minio.example.com:9000")]
    // empty/awkward values
    [InlineData("", "", S3Provider.CloudflareR2, "", "abc123account")]
    public void Settings_RoundTrip(string accessKey, string secretKey, S3Provider provider, string region, string endpointOrAccount)
    {
      string segment = S3PathCodec.SerializeSettings(accessKey, secretKey, provider, region, endpointOrAccount);

      S3PathCodec.ParseSettings(segment, out string ak, out string sk, out S3Provider prov, out string reg, out string ep);

      Assert.Equal(accessKey, ak);
      Assert.Equal(secretKey, sk);   // '+' must come back as '+', not as a space
      Assert.Equal(provider, prov);
      Assert.Equal(region, reg);
      Assert.Equal(endpointOrAccount, ep);
    }

    [Fact]
    public void Settings_PlusInSecret_IsNotDecodedToSpace()
    {
      string segment = S3PathCodec.SerializeSettings("ak", "a+b+c", S3Provider.Aws, "us-east-1", "");
      // the literal '+' must have been percent-encoded, not left bare
      Assert.Contains("%2B", segment, StringComparison.OrdinalIgnoreCase);
      S3PathCodec.ParseSettings(segment, out _, out string sk, out _, out _, out _);
      Assert.Equal("a+b+c", sk);
    }

    [Fact]
    public void ParseSettings_MissingMarker_Throws()
    {
      Assert.Throws<FormatException>(() => S3PathCodec.ParseSettings("not-a-set-segment", out _, out _, out _, out _, out _));
    }

    // ---- path parsing ----

    [Fact]
    public void ParsePath_SplitsSettingsBucketAndKey()
    {
      string settings = S3PathCodec.SerializeSettings("ak", "sk", S3Provider.Aws, "us-east-1", "");
      string path = S3PathCodec.BuildPath(settings, "my-bucket", "folder/db.kdbx");

      S3PathCodec.ParsePath(path, out string parsedSettings, out string bucket, out string key);

      Assert.Equal(settings, parsedSettings);
      Assert.Equal("my-bucket", bucket);
      Assert.Equal("folder/db.kdbx", key);
    }

    [Fact]
    public void ParsePath_BucketWithoutKey_YieldsEmptyKey()
    {
      string settings = S3PathCodec.SerializeSettings("ak", "sk", S3Provider.Aws, "us-east-1", "");
      S3PathCodec.ParsePath("s3://" + settings + "#only-bucket", out _, out string bucket, out string key);
      Assert.Equal("only-bucket", bucket);
      Assert.Equal("", key);
    }

    [Fact]
    public void ParsePath_MissingHash_Throws()
    {
      Assert.Throws<FormatException>(() => S3PathCodec.ParsePath("s3://SETnohashhere", out _, out _, out _));
    }

    // ---- full round-trip: dialog values -> path -> back ----

    [Fact]
    public void BuildFullPath_RoundTripsThroughParse()
    {
      string path = S3PathCodec.BuildFullPath(S3Provider.Custom, "us-east-1",
          "https://minio.example.com:9000", "bucket", "AK:1", "S+K/2=", "/leading/slash/db.kdbx");

      S3PathCodec.ParsePath(path, out string settings, out string bucket, out string key);
      S3PathCodec.ParseSettings(settings, out string ak, out string sk, out S3Provider prov, out string reg, out string ep);

      Assert.Equal("bucket", bucket);
      Assert.Equal("leading/slash/db.kdbx", key);   // BuildFullPath trims a leading '/'
      Assert.Equal("AK:1", ak);
      Assert.Equal("S+K/2=", sk);
      Assert.Equal(S3Provider.Custom, prov);
      Assert.Equal("us-east-1", reg);
      Assert.Equal("https://minio.example.com:9000", ep);
    }

    // ---- preview URL per provider / addressing style ----

    [Theory]
    [InlineData(S3Provider.Aws, "us-west-2", "", "mybucket", "db.kdbx", "https://mybucket.s3.us-west-2.amazonaws.com/db.kdbx")]
    [InlineData(S3Provider.Aws, "", "", "mybucket", "db.kdbx", "https://mybucket.s3.amazonaws.com/db.kdbx")]
    [InlineData(S3Provider.Wasabi, "eu-central-1", "", "b", "k.kdbx", "https://b.s3.eu-central-1.wasabisys.com/k.kdbx")]
    [InlineData(S3Provider.BackblazeB2, "us-west-004", "", "b", "k.kdbx", "https://b.s3.us-west-004.backblazeb2.com/k.kdbx")]
    [InlineData(S3Provider.CloudflareR2, "", "acct123", "b", "k.kdbx", "https://acct123.r2.cloudflarestorage.com/b/k.kdbx")]
    [InlineData(S3Provider.Custom, "", "https://minio.example.com:9000", "b", "k.kdbx", "https://minio.example.com:9000/b/k.kdbx")]
    public void BuildPreviewUrl_MatchesAddressingStyle(S3Provider provider, string region, string endpointOrAccount,
        string bucket, string objectKey, string expected)
    {
      Assert.Equal(expected, S3PathCodec.BuildPreviewUrl(provider, region, endpointOrAccount, bucket, objectKey));
    }

    [Fact]
    public void BuildPreviewUrl_TrimsLeadingSlashOnKey()
    {
      Assert.Equal("https://b.s3.amazonaws.com/folder/k.kdbx",
          S3PathCodec.BuildPreviewUrl(S3Provider.Aws, "", "", "b", "/folder/k.kdbx"));
    }

    // ---- bucket-in-key detection ----

    [Theory]
    [InlineData("mybucket", "mybucket/db.kdbx", true)]    // repeated bucket prefix
    [InlineData("mybucket", "/mybucket/db.kdbx", true)]   // leading slash still detected
    [InlineData("mybucket", "db.kdbx", false)]
    [InlineData("mybucket", "mybucketX/db.kdbx", false)]  // prefix but not a path segment
    [InlineData("", "anything/db.kdbx", false)]           // no bucket -> no warning
    public void KeyRepeatsBucket_DetectsDuplication(string bucket, string objectKey, bool expected)
    {
      Assert.Equal(expected, S3PathCodec.KeyRepeatsBucket(bucket, objectKey));
    }
  }
}
