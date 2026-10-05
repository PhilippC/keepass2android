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
using System.Net;

namespace keepass2android.Io
{
  /// <summary>S3 / S3-compatible provider. The integer values are persisted in the path, so don't reorder.</summary>
  public enum S3Provider
  {
    Aws = 0,
    Wasabi = 1,
    BackblazeB2 = 2,
    CloudflareR2 = 3,
    Custom = 4
  }

  /// <summary>
  /// Pure, platform-agnostic codec for the s3:// path format used by the S3 backend. This is the
  /// most bug-prone part of the feature (string surgery + URL-encoding), so it lives in its own
  /// library with no Android/KeePassLib dependencies and is unit-tested (Kp2aS3PathCodec.Tests).
  /// </summary>
  /// <remarks>
  /// Path format:
  /// <code>
  /// s3://SET&lt;accessKey&gt;:&lt;secret&gt;:&lt;provider&gt;:&lt;region&gt;:&lt;endpointOrAccount&gt;#&lt;bucket&gt;/&lt;objectKey&gt;
  /// </code>
  /// The five settings tokens after the "SET" marker are each URL-encoded so a ':' '#' or '/'
  /// inside a value can't break parsing; the first '#' separates the encoded settings segment
  /// from the raw "&lt;bucket&gt;/&lt;objectKey&gt;" location.
  /// </remarks>
  public static class S3PathCodec
  {
    public const string ProtocolId = "s3";
    public const string SettingsPrefix = "SET";
    public const string SettingsPostFix = "#";
    public const char Separator = ':';

    /// <summary>
    /// Serializes the connection settings into the (URL-encoded) "SET..." settings segment.
    /// Each token is URL-encoded so a ':' '#' or '/' inside a value can't break parsing.
    /// </summary>
    public static string SerializeSettings(string? accessKey, string? secretKey, S3Provider provider,
        string? region, string? endpointOrAccount)
    {
      return SettingsPrefix +
             WebUtility.UrlEncode(accessKey ?? "") + Separator +
             WebUtility.UrlEncode(secretKey ?? "") + Separator +
             (int)provider + Separator +
             WebUtility.UrlEncode(region ?? "") + Separator +
             WebUtility.UrlEncode(endpointOrAccount ?? "");
    }

    /// <summary>Parses a "SET..." settings segment back into its fields (inverse of <see cref="SerializeSettings"/>).</summary>
    public static void ParseSettings(string settingsSegment, out string accessKey, out string secretKey,
        out S3Provider provider, out string region, out string endpointOrAccount)
    {
      if (settingsSegment == null || !settingsSegment.StartsWith(SettingsPrefix, StringComparison.Ordinal))
        throw new FormatException("unexpected settings in S3 path (missing '" + SettingsPrefix + "' marker)");
      string body = settingsSegment.Substring(SettingsPrefix.Length);
      string[] tokens = body.Split(Separator);
      if (tokens.Length < 5)
        throw new FormatException("unexpected settings in S3 path (expected 5 tokens, got " + tokens.Length + ")");
      accessKey = WebUtility.UrlDecode(tokens[0]);
      secretKey = WebUtility.UrlDecode(tokens[1]);
      provider = (S3Provider)int.Parse(tokens[2]);
      region = WebUtility.UrlDecode(tokens[3]);
      endpointOrAccount = WebUtility.UrlDecode(tokens[4]);
    }

    /// <summary>
    /// Splits an s3:// path into its (still URL-encoded) settings segment, bucket and object key.
    /// This is the single path parser; the malformed-path guard lives here in one place.
    /// </summary>
    public static void ParsePath(string path, out string settings, out string bucket, out string key)
    {
      int schemeSeparatorIndex = path.IndexOf("://", StringComparison.Ordinal);
      string rest = path.Substring(schemeSeparatorIndex + 3);
      int settingsSeparatorIndex = rest.IndexOf(SettingsPostFix, StringComparison.Ordinal);
      if (settingsSeparatorIndex < 0)
        throw new FormatException("unexpected S3 path (missing '" + SettingsPostFix + "' settings separator)");
      settings = rest.Substring(0, settingsSeparatorIndex);
      string afterSettings = rest.Substring(settingsSeparatorIndex + 1);
      int firstSlashIndex = afterSettings.IndexOf('/');
      if (firstSlashIndex < 0)
      {
        bucket = afterSettings;
        key = "";
      }
      else
      {
        bucket = afterSettings.Substring(0, firstSlashIndex);
        key = afterSettings.Substring(firstSlashIndex + 1);
      }
    }

    /// <summary>Assembles a full s3:// path from a (serialized) settings segment, bucket and key.</summary>
    public static string BuildPath(string settings, string bucket, string key)
    {
      return ProtocolId + "://" + settings + SettingsPostFix + bucket + "/" + key;
    }

    /// <summary>
    /// Assembles a full s3:// path from the values entered in the credentials dialog.
    /// <paramref name="objectKey"/> is the fully qualified object key inside the bucket
    /// (S3 has no directories), e.g. "passwords.kdbx" or "folder/passwords.kdbx".
    /// </summary>
    public static string BuildFullPath(S3Provider provider, string? region, string? endpointOrAccount,
        string bucket, string? accessKey, string? secretKey, string? objectKey)
    {
      string settings = SerializeSettings(accessKey, secretKey, provider, region ?? "", endpointOrAccount ?? "");
      string key = (objectKey ?? "").TrimStart('/');
      return BuildPath(settings, bucket, key);
    }

    /// <summary>
    /// Builds a human-readable https URL preview of the object the current dialog values point to.
    /// The URL shape follows the addressing style each provider uses (matching ForcePathStyle in
    /// the S3 client): AWS/Wasabi/B2 use virtual-hosted style (bucket as a sub-domain of the
    /// regional host); R2 and Custom/MinIO use path style (bucket as the first path segment under
    /// the endpoint host). Placeholders in angle brackets are shown for values not yet entered.
    /// </summary>
    public static string BuildPreviewUrl(S3Provider provider, string? region, string? endpointOrAccount,
        string? bucket, string? objectKey)
    {
      region = (region ?? "").Trim();
      endpointOrAccount = (endpointOrAccount ?? "").Trim();
      bucket = (bucket ?? "").Trim();
      objectKey = (objectKey ?? "").Trim().TrimStart('/');
      string bucketOrPlaceholder = bucket.Length == 0 ? "<bucket>" : bucket;

      switch (provider)
      {
        //AWS/Wasabi/B2: virtual-hosted style -> https://<bucket>.<regional-host>/<key>
        case S3Provider.Aws:
        {
          string host = region.Length == 0 ? "s3.amazonaws.com" : "s3." + region + ".amazonaws.com";
          return "https://" + bucketOrPlaceholder + "." + host + "/" + objectKey;
        }
        case S3Provider.Wasabi:
        {
          string r = region.Length == 0 ? "<region>" : region;
          return "https://" + bucketOrPlaceholder + ".s3." + r + ".wasabisys.com/" + objectKey;
        }
        case S3Provider.BackblazeB2:
        {
          string r = region.Length == 0 ? "<region>" : region;
          return "https://" + bucketOrPlaceholder + ".s3." + r + ".backblazeb2.com/" + objectKey;
        }
        //R2: path style under the account host -> https://<account>.r2.cloudflarestorage.com/<bucket>/<key>
        case S3Provider.CloudflareR2:
        {
          string acct = endpointOrAccount.Length == 0 ? "<account-id>" : endpointOrAccount;
          return "https://" + acct + ".r2.cloudflarestorage.com/" + bucketOrPlaceholder + "/" + objectKey;
        }
        //Custom/MinIO: path style under the user's endpoint -> <endpoint>/<bucket>/<key> (ForcePathStyle)
        case S3Provider.Custom:
        {
          string ep = endpointOrAccount.TrimEnd('/');
          if (ep.Length == 0) ep = "<endpoint>";
          return ep + "/" + bucketOrPlaceholder + "/" + objectKey;
        }
        default:
          return "";
      }
    }

    /// <summary>
    /// True if the object key starts with "&lt;bucket&gt;/" — a common mistake where the user repeats
    /// the bucket name in the key, producing a "bucket/bucket/..." path. Comparison is ordinal.
    /// </summary>
    public static bool KeyRepeatsBucket(string? bucket, string? objectKey)
    {
      if (string.IsNullOrEmpty(bucket))
        return false;
      string trimmedKey = (objectKey ?? "").TrimStart('/');
      return trimmedKey.StartsWith(bucket + "/", StringComparison.Ordinal);
    }
  }
}
