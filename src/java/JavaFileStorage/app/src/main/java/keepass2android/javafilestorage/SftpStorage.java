package keepass2android.javafilestorage;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeSet;

import com.jcraft.jsch.Channel;
import com.jcraft.jsch.ChannelSftp;
import com.jcraft.jsch.ChannelSftp.LsEntry;
import com.jcraft.jsch.JSch;
import com.jcraft.jsch.JSchException;
import com.jcraft.jsch.Session;
import com.jcraft.jsch.SftpATTRS;
import com.jcraft.jsch.SftpException;
import com.jcraft.jsch.UserInfo;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.util.Log;

@SuppressWarnings("unused")  // Exposed by JavaFileStorageBindings
public class SftpStorage extends JavaFileStorageBase {
	@FunctionalInterface
	interface ValueResolver<T> {
		/**
		 * Takes a raw value and resolves it to either a String containing the String representation
		 * of that value, or null. The latter signifying that the raw value could not be "resolved".
		 *
		 * @param value
		 * @return String, or null if not resolvable
		 */
		String resolve(T value);
	}

	public static final int DEFAULT_SFTP_PORT = 22;
	public static final int UNSET_SFTP_CONNECT_TIMEOUT = -1;
	private static final String SFTP_CONNECT_TIMEOUT_OPTION_NAME = "connectTimeout";
	private static final String SFTP_KEYNAME_OPTION_NAME = "key";
	private static final String SFTP_KEYPASSPHRASE_OPTION_NAME = "phrase";

	public static final String SSH_CFG_KEX = "kex";
	private static final String[] SSH_CFG_KEYS = new String[] {
			SSH_CFG_KEX
	};

	private static final ValueResolver<Integer> cTimeoutResolver = c ->
			c == null || c == UNSET_SFTP_CONNECT_TIMEOUT ? null : String.valueOf(c);

	private static final ValueResolver<String> nonBlankStringResolver = s ->
			s == null || s.isBlank() ? null : s;

	private JSch jsch;

	public class ConnectionInfo
	{
		public String host;
		public String username;
		public String password;
		public String localPath;
		public String keyName;
		public String keyPassphrase;
		public int port;
		public int connectTimeoutSec = UNSET_SFTP_CONNECT_TIMEOUT;
		public final Map<String, String> configOpts = new HashMap<>();


		public String toString() {
			return "ConnectionInfo{host=" + host + ",port=" + port + ",user=" + username +
					",pwd=<hidden>,localPath=" + localPath + ",key=" + keyName +
					",phrase=<hidden>,connectTimeout=" + connectTimeoutSec +
					",cfgOpts=" + configOpts +
					"}";
		}
	}

	private static Map<String, String> buildOptionMap(ConnectionInfo ci, boolean includeSensitive) {
		OptionMapBuilder b = new OptionMapBuilder()
				.addOption(SFTP_CONNECT_TIMEOUT_OPTION_NAME, ci.connectTimeoutSec, cTimeoutResolver)
				.addOption(SFTP_KEYNAME_OPTION_NAME, ci.keyName, nonBlankStringResolver);
		// Assume all config options are not sensitive and use the same resolver...
		for (Map.Entry<String, String> entry : ci.configOpts.entrySet()) {
			b.addOption(entry.getKey(), entry.getValue(), nonBlankStringResolver);
		}
		if (includeSensitive) {
			b.addOption(SFTP_KEYPASSPHRASE_OPTION_NAME, ci.keyPassphrase, nonBlankStringResolver);
		}
		return b.build();
	}

	Context _appContext;
	private final SftpPublicPrivateKeyUtils _keyUtils;

	public SftpStorage(Context appContext) {
		_appContext = appContext;
		_keyUtils = new SftpPublicPrivateKeyUtils(getBaseDir());
	}

	private static final String SFTP_PROTOCOL_ID = "sftp";

	@Override
	public boolean checkForFileChangeFast(String path,
			String previousFileVersion) throws Exception {
		String currentVersion = getCurrentFileVersionFast(path);
		if (currentVersion == null)
			return false;
		return currentVersion.equals(previousFileVersion) == false;
	}

	@Override
	public String getCurrentFileVersionFast(String path) {

		return null; // no simple way to get the version "fast"
	}

	@Override
	public InputStream openFileForRead(String path) throws Exception {
		ConnectionInfo cInfo = splitStringToConnectionInfo(path);
		ChannelSftp c = init(cInfo);

		try {
			byte[] buff = new byte[8000];

			int bytesRead = 0;

			InputStream in = c.get(cInfo.localPath);
			ByteArrayOutputStream bao = new ByteArrayOutputStream();

			while ((bytesRead = in.read(buff)) != -1) {
				bao.write(buff, 0, bytesRead);
			}

			byte[] data = bao.toByteArray();

			ByteArrayInputStream bin = new ByteArrayInputStream(data);
			c.getSession().disconnect();

			return bin;

		} catch (Exception e) {
			tryDisconnect(c);
			throw convertException(e);
		}
	}

	private void tryDisconnect(ChannelSftp c) {
		try {
			c.getSession().disconnect();
		} catch (JSchException je) {

		}
	}

	@Override
	public void uploadFile(String path, byte[] data, boolean writeTransactional)
			throws Exception {

		ConnectionInfo cInfo = splitStringToConnectionInfo(path);
		ChannelSftp c = init(cInfo);
		try {
			InputStream in = new ByteArrayInputStream(data);
			String targetPath = cInfo.localPath;
			if (writeTransactional)
			{
				//upload to temporary location:
				String tmpPath = targetPath + ".tmp";
				c.put(in, tmpPath);
				//remove previous file:
				try
				{
					c.rm(targetPath);
				}
				catch (SftpException e)
				{
					//ignore. Can happen if file didn't exist before
				}
				//rename tmp to target path:
				c.rename(tmpPath, targetPath);
			}
			else
			{
				c.put(in, targetPath);
			}

			tryDisconnect(c);
		} catch (Exception e) {
			tryDisconnect(c);
			throw e;
		}

	}

	@Override
	public String createFolder(String parentPath, String newDirName)
			throws Exception {
		ConnectionInfo cInfo = splitStringToConnectionInfo(parentPath);
		try {
			ChannelSftp c = init(cInfo);
			String newPath = concatPaths(cInfo.localPath, newDirName);
			c.mkdir(newPath);
			tryDisconnect(c);

			return buildFullPath(cInfo.host, cInfo.port, newPath,
					cInfo.username, cInfo.password, cInfo.connectTimeoutSec,
					cInfo.keyName, cInfo.keyPassphrase,
					cInfo.configOpts.get(SSH_CFG_KEX));
		} catch (Exception e) {
			throw convertException(e);
		}

	}

	private String extractUserPwdHostPort(String path) {
	    String withoutProtocol = path
				.substring(getProtocolPrefix().length());
		return withoutProtocol.substring(0, withoutProtocol.indexOf("/"));
	}

	private String extractSessionPath(String newPath) {
		String withoutProtocol = newPath
				.substring(getProtocolPrefix().length());
		int pathStartIdx = withoutProtocol.indexOf("/");
		int pathEndIdx = withoutProtocol.indexOf("?");
		if (pathEndIdx < 0) {
			pathEndIdx = withoutProtocol.length();
		}
		return withoutProtocol.substring(pathStartIdx, pathEndIdx);
	}

	private Map<String, String> extractOptionsMap(String path) throws UnsupportedEncodingException {
		String withoutProtocol = path
				.substring(getProtocolPrefix().length());

		Map<String, String> options = new HashMap<>();

		int extraOptsIdx = withoutProtocol.indexOf("?");
		if (extraOptsIdx > 0 && extraOptsIdx + 1 < withoutProtocol.length()) {
			String optsString = withoutProtocol.substring(extraOptsIdx + 1);
			String[] parts = optsString.split("&");
			for (String p : parts) {
				int sepIdx = p.indexOf('=');
				if (sepIdx > 0) {
					String key = decode(p.substring(0, sepIdx));
					String value = decode(p.substring(sepIdx + 1));
					options.put(key, value);
				} else {
					options.put(decode(p), "true");
				}
			}
		}
		return options;
	}

	private String concatPaths(String parentPath, String newDirName) {
		StringBuilder fp = new StringBuilder(parentPath);
		if (!parentPath.endsWith("/"))
			fp.append("/");
		return fp.append(newDirName).toString();
	}

	@Override
	public String createFilePath(final String parentUri, String newFileName)
			throws Exception {

		String parentPath = parentUri;
		String params = null;
		int paramsIdx = parentUri.lastIndexOf("?");
		if (paramsIdx > 0) {
			params = parentUri.substring(paramsIdx);
			parentPath = parentPath.substring(0, paramsIdx);
		}

		String newPath = concatPaths(parentPath, newFileName);

		if (params != null) {
			newPath += params;
		}
		return newPath;
	}

	@Override
	public List<FileEntry> listFiles(String parentPath) throws Exception {
		ConnectionInfo cInfo = splitStringToConnectionInfo(parentPath);
		ChannelSftp c = init(cInfo);

		return listFiles(parentPath, c);
	}

	private void setFromAttrs(FileEntry fileEntry, SftpATTRS attrs) {
		fileEntry.isDirectory = attrs.isDir();
		fileEntry.canRead = true; // currently not inferred from the
									// permissions.
		fileEntry.canWrite = true; // currently not inferred from the
									// permissions.
		fileEntry.lastModifiedTime = ((long) attrs.getMTime()) * 1000;
		if (fileEntry.isDirectory)
			fileEntry.sizeInBytes = 0;
		else
			fileEntry.sizeInBytes = attrs.getSize();
	}

	private Exception convertException(Exception e) {

		if (SftpException.class.isAssignableFrom(e.getClass()) )
		{
			SftpException sftpEx = (SftpException)e;
			if (sftpEx.id == ChannelSftp.SSH_FX_NO_SUCH_FILE)
				return new FileNotFoundException(sftpEx.getMessage());
		}

		return e;

	}

	@Override
	public FileEntry getFileEntry(String filename) throws Exception {
		ConnectionInfo cInfo = splitStringToConnectionInfo(filename);
		ChannelSftp c = init(cInfo);
		try {
			FileEntry fileEntry = new FileEntry();
			SftpATTRS attr = c.stat(cInfo.localPath);
			setFromAttrs(fileEntry, attr);

			// Full URI
			fileEntry.path = filename;

			fileEntry.displayName = getFilename(cInfo.localPath);

			tryDisconnect(c);

			return fileEntry;
		} catch (Exception e) {
			logDebug("Exception in getFileEntry! " + e);
			tryDisconnect(c);
			throw convertException(e);
		}
	}

	@Override
	public void delete(String path) throws Exception {
		ConnectionInfo cInfo = splitStringToConnectionInfo(path);
		ChannelSftp c = init(cInfo);

		delete(path, c);
	}

	private void delete(String path, ChannelSftp c) throws Exception {
		String sessionLocalPath = extractSessionPath(path);
		try {
			if (c.stat(sessionLocalPath).isDir())
			{
				List<FileEntry> contents = listFiles(path, c);
				for (FileEntry fe: contents)
				{
					delete(fe.path, c);
				}
				c.rmdir(sessionLocalPath);
			}
			else
			{
				c.rm(sessionLocalPath);
			}
		} catch (Exception e) {
			tryDisconnect(c);
			throw convertException(e);
		}

	}

	private List<FileEntry> listFiles(String path, ChannelSftp c) throws Exception {

		try {
			List<FileEntry> res = new ArrayList<FileEntry>();
			@SuppressWarnings("rawtypes")
			java.util.Vector vv = c.ls(extractSessionPath(path));
			if (vv != null) {
				for (int ii = 0; ii < vv.size(); ii++) {

					Object obj = vv.elementAt(ii);
					if (obj instanceof com.jcraft.jsch.ChannelSftp.LsEntry) {
						LsEntry lsEntry = (com.jcraft.jsch.ChannelSftp.LsEntry) obj;

						if ((lsEntry.getFilename().equals("."))
								||(lsEntry.getFilename().equals(".."))
								)
							continue;

						FileEntry fileEntry = new FileEntry();
						fileEntry.displayName = lsEntry.getFilename();
						fileEntry.path = createFilePath(path, fileEntry.displayName);
						SftpATTRS attrs = lsEntry.getAttrs();
						setFromAttrs(fileEntry, attrs);
						res.add(fileEntry);
					}

				}
			}
			return res;
		} catch (Exception e) {
			tryDisconnect(c);
			throw convertException(e);
		}
	}

	@Override
	public void startSelectFile(
			JavaFileStorage.FileStorageSetupInitiatorActivity activity,
			boolean isForSave, int requestCode) {
		activity.performManualFileSelect(isForSave, requestCode, getProtocolId());
	}

	@Override
	protected String decode(String encodedString)
			throws UnsupportedEncodingException {
		return java.net.URLDecoder.decode(encodedString, UTF_8);
	}

	@Override
	protected String encode(final String unencoded)
			throws UnsupportedEncodingException {
		return java.net.URLEncoder.encode(unencoded, UTF_8);
	}


	ChannelSftp init(ConnectionInfo cInfo) throws JSchException, UnsupportedEncodingException {
		jsch = new JSch();

		Log.d("KP2AJFS", "init SFTP");

		String base_dir = getBaseDir();
		jsch.setKnownHosts(base_dir + "/known_hosts");

		String key_filepath = _keyUtils.resolveKeyFilePath(jsch, cInfo.keyName);

		try {
			jsch.addIdentity(key_filepath);
		} catch (java.lang.Exception e) {

		}

		Log.e("KP2AJFS[thread]", "getting session...");
		Session session = jsch.getSession(cInfo.username, cInfo.host, cInfo.port);
		Log.e("KP2AJFS", "creating SftpUserInfo");
		UserInfo ui = new SftpUserInfo(cInfo.password, cInfo.keyPassphrase, _appContext);
		session.setUserInfo(ui);

		session.setConfig("PreferredAuthentications", "publickey,password");

		for (Map.Entry<String, String> e : cInfo.configOpts.entrySet()) {
			Log.d("KP2AJFS", "Setting SSH config: " + e.getKey() + "=" + e.getValue());
			session.setConfig(e.getKey(), e.getValue());
		}

		sessionConnect(session, cInfo);

		Channel channel = session.openChannel("sftp");
		channel.connect();
		ChannelSftp c = (ChannelSftp) channel;

		return c;

	}

	private void sessionConnect(Session session, ConnectionInfo ci) throws JSchException {
		if (ci.connectTimeoutSec != UNSET_SFTP_CONNECT_TIMEOUT) {
			session.connect(ci.connectTimeoutSec * 1000);
		} else {
			session.connect();
		}
	}

	private String getBaseDir() {
		return _appContext.getFilesDir().getAbsolutePath();
	}

	public boolean deleteCustomKey(String keyName) throws FileNotFoundException {
		return _keyUtils.deleteCustomKey(keyName);
	}

	public String[] getCustomKeyNames() {
		return _keyUtils.getCustomKeyNames();
	}

	@SuppressWarnings("unused")  // Exposed by JavaFileStorageBindings
	public String createKeyPair() throws IOException, JSchException {
		return _keyUtils.createKeyPair(jsch);
	}

	@SuppressWarnings("unused")  // Exposed by JavaFileStorageBindings
	public void savePrivateKeyContent(String keyName, String keyContent) throws IOException, Exception {
		_keyUtils.savePrivateKeyContent(keyName, keyContent);
	}

	@SuppressWarnings("unused")  // Exposed by JavaFileStorageBindings
	public void setJschLogging(boolean enabled, String logFilename) {
		if (enabled) {
			JSch.setLogger(new Kp2aJSchLogger(logFilename));
		} else {
			JSch.setLogger(null);
		}
	}

	/**
	 * Exposed for testing purposes only.
	 * @param keyName
	 * @return
	 */
	public String sanitizeCustomKeyName(String keyName) {
		return _keyUtils.getSanitizedCustomKeyName(keyName);
	}

	/**
	 * Exposed for testing purposes only.
	 * @param keyContent
	 * @return
	 * @throws Exception
	 */
	public String getValidatedCustomKeyContent(String keyContent) throws Exception {
		return _keyUtils.getValidatedCustomKeyContent(keyContent);
	}


	public ConnectionInfo splitStringToConnectionInfo(String filename)
			throws UnsupportedEncodingException {

		ConnectionInfo ci = new ConnectionInfo();
		ci.host = extractUserPwdHostPort(filename);

		String userPwd = ci.host.substring(0, ci.host.indexOf('@'));
		int sepIdx = userPwd.indexOf(":");
		if (sepIdx > 0) {
			ci.username = decode(userPwd.substring(0, sepIdx));
			ci.password = decode(userPwd.substring(sepIdx + 1));
		} else {
			ci.username = userPwd;
			ci.password = null;
		}

		ci.host = ci.host.substring(ci.host.indexOf('@') + 1);
		ci.port = DEFAULT_SFTP_PORT;

		int portSeparatorIndex = ci.host.lastIndexOf(':');
		if (portSeparatorIndex >= 0)
		{
			ci.port = Integer.parseInt(ci.host.substring(portSeparatorIndex + 1));
			ci.host = ci.host.substring(0, portSeparatorIndex);
		}
		// Encode/decode required to support IPv6 (colons break host:port parse logic)
		// See Bug #2350
		ci.host = decode(ci.host);

		ci.localPath = extractSessionPath(filename);

		Map<String, String> options = extractOptionsMap(filename);

		if (options.containsKey(SFTP_CONNECT_TIMEOUT_OPTION_NAME)) {
			String optVal = options.get(SFTP_CONNECT_TIMEOUT_OPTION_NAME);
			try {
				ci.connectTimeoutSec = Integer.parseInt(optVal);
			} catch (NumberFormatException nan) {
				logDebug(SFTP_CONNECT_TIMEOUT_OPTION_NAME + " option not a number: " + optVal);
			}
		}
		if (options.containsKey(SFTP_KEYNAME_OPTION_NAME)) {
			ci.keyName = options.get(SFTP_KEYNAME_OPTION_NAME);
		}
		if (options.containsKey(SFTP_KEYPASSPHRASE_OPTION_NAME)) {
			ci.keyPassphrase = options.get(SFTP_KEYPASSPHRASE_OPTION_NAME);
		}

		// TODO: Support for prepending/appending config values (instead of complete replacement)?
		for (String cfgKey : SSH_CFG_KEYS) {
			if (options.containsKey(cfgKey)) {
				ci.configOpts.put(cfgKey, options.get(cfgKey));
			}
		}

		return ci;
	}

	@Override
	public void prepareFileUsage(JavaFileStorage.FileStorageSetupInitiatorActivity activity, String path, int requestCode, boolean alwaysReturnSuccess) {
		Intent intent = new Intent();
		intent.putExtra(EXTRA_PATH, path);
		activity.onImmediateResult(requestCode, RESULT_FILEUSAGE_PREPARED, intent);
	}

	@Override
	public String getProtocolId() {
		return SFTP_PROTOCOL_ID;
	}

	@Override
	public void onResume(JavaFileStorage.FileStorageSetupActivity setupAct) {

	}

	@Override
	public boolean requiresSetup(String path) {
		return false;
	}

	@Override
	public void onCreate(FileStorageSetupActivity activity,
			Bundle savedInstanceState) {

	}

	@Override
	public String getDisplayName(String path) {
		try
		{
			ConnectionInfo ci = splitStringToConnectionInfo(path);
			StringBuilder dName = new StringBuilder(getProtocolPrefix())
					.append(ci.username)
					.append("@")
					.append(ci.host)
					.append(ci.localPath);
			appendOptions(dName, buildOptionMap(ci, false));
			return dName.toString();
		}
		catch (Exception e)
		{
			return extractSessionPath(path);
		}
	}

	@Override
	public String getFilename(String path) throws Exception {
		if (path.endsWith("/"))
			path = path.substring(0, path.length()-1);
		int lastIndex = path.lastIndexOf("/");
		if (lastIndex >= 0)
			return path.substring(lastIndex + 1);
		else
			return path;
	}

	@Override
	public void onStart(FileStorageSetupActivity activity) {

	}

	@Override
	public void onActivityResult(FileStorageSetupActivity activity,
			int requestCode, int resultCode, Intent data) {


	}

	public String buildFullPath(String host, int port, String localPath,
										String username, String password,
										int connectTimeoutSec,
										String keyName, String keyPassphrase,
										String kexAlgorithms)
			throws UnsupportedEncodingException {

		StringBuilder uri = new StringBuilder(getProtocolPrefix()).append(encode(username));
		if (password != null) {
			uri.append(":").append(encode(password));
		}
		uri.append("@");
		// Encode/decode required to support IPv6 (colons break host:port parse logic)
		// See Bug #2350
		uri.append(encode(host));

		if (port != DEFAULT_SFTP_PORT) {
			uri.append(":").append(port);
		}
		if (localPath != null && localPath.startsWith("/")) {
			uri.append(localPath);
		}

		appendOptions(uri, new OptionMapBuilder()
				.addOption(SFTP_CONNECT_TIMEOUT_OPTION_NAME, connectTimeoutSec, cTimeoutResolver)
				.addOption(SFTP_KEYNAME_OPTION_NAME, keyName, nonBlankStringResolver)
				.addOption(SFTP_KEYPASSPHRASE_OPTION_NAME, keyPassphrase, nonBlankStringResolver)
				.addOption(SSH_CFG_KEX, kexAlgorithms, nonBlankStringResolver)
				.build());

		// FIXME: Remove this!
		Log.d("KP2AJFS", "buildFullPath returns uri: " + uri.toString());
		// FIXME <end>

		return uri.toString();
	}

	private void appendOptions(StringBuilder uri, Map<String, String> opts)
			throws UnsupportedEncodingException {

		boolean first = true;
		// Sort for stability/consistency
		Set<Map.Entry<String, String>> sortedEntries = new TreeSet<>(new EntryComparator<>());
		sortedEntries.addAll(opts.entrySet());
		for (Map.Entry<String, String> me : sortedEntries) {
			if (first) {
				uri.append("?");
				first = false;
			} else {
				uri.append("&");
			}
			uri.append(encode(me.getKey())).append("=").append(encode(me.getValue()));
		}
	}


	@Override
	public void prepareFileUsage(Context appContext, String path) {
		//nothing to do

	}

	/**
	 * A comparator that compares Map.Entry objects by their keys, via natural ordering.
	 *
	 * @param <T> the Map.Entry key type, that must implement Comparable.
	 */
	private static class EntryComparator<T extends Comparable<T>> implements Comparator<Map.Entry<T, ?>> {
		@Override
		public int compare(Map.Entry<T, ?> o1, Map.Entry<T, ?> o2) {
			return o1.getKey().compareTo(o2.getKey());
		}
	}

	private static class OptionMapBuilder {
		private final Map<String, String> options = new HashMap<>();

		/**
		 * Attempts to add a raw value <code>oVal</code> to the underlying option map with key <code>oName</code>
		 * iff the <code>resolver</code> produces a non-null output when invoked using the raw value.
		 *
		 * @param oName the name/key associated with the value, if added
		 * @param oVal the raw value attempting to be added
		 * @param resolver the resolver that determines if the value will be added
		 *
		 * @return OptionMapBuilder (updated)
		 * @param <T> the raw value type
		 */
		<T> OptionMapBuilder addOption(final String oName, T oVal, ValueResolver<T> resolver) {
			String resolved = resolver.resolve(oVal);
			if (resolved != null) {
				options.put(oName, resolved);
			}
			return this;
		}

		Map<String, String> build() {
			return new HashMap<>(options);
		}
	}
}
