package keepass2android.javafilestorage;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.io.UnsupportedEncodingException;
import java.util.ArrayList;
import java.util.List;

import com.jcraft.jsch.Channel;
import com.jcraft.jsch.ChannelSftp;
import com.jcraft.jsch.ChannelSftp.LsEntry;
import com.jcraft.jsch.JSch;
import com.jcraft.jsch.JSchException;
import com.jcraft.jsch.KeyPair;
import com.jcraft.jsch.Session;
import com.jcraft.jsch.SftpATTRS;
import com.jcraft.jsch.SftpException;
import com.jcraft.jsch.UserInfo;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;

public class SftpStorage extends JavaFileStorageBase {

	public static final int DEFAULT_SFTP_PORT = 22;
	JSch jsch;

	public class ConnectionInfo
	{
		public String host;
		public String username;
		public String password;
		public String localPath;
		public int port;
	}
	Context _appContext;

	public SftpStorage(Context appContext) {
		_appContext = appContext;

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

		ChannelSftp c = init(path);

		try {
			byte[] buff = new byte[8000];

			int bytesRead = 0;

			InputStream in = c.get(extractSessionPath(path));
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

		ChannelSftp c = init(path);
		try {
			InputStream in = new ByteArrayInputStream(data);
			String targetPath = extractSessionPath(path);
			if (writeTransactional)
			{
				//upload to temporary location:
				String tmpPath = targetPath+".tmp";
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

		try {
			ChannelSftp c = init(parentPath);
			String newPath = concatPaths(parentPath, newDirName);
			c.mkdir(extractSessionPath(newPath));
			tryDisconnect(c);
			return newPath;
		} catch (Exception e) {
			throw convertException(e);
		}

	}

	private String extractSessionPath(String newPath) {
		String withoutProtocol = newPath
				.substring(getProtocolPrefix().length());
		return withoutProtocol.substring(withoutProtocol.indexOf("/"));
	}
	
	private String extractUserPwdHost(String path) {
		String withoutProtocol = path
				.substring(getProtocolPrefix().length());
		return withoutProtocol.substring(0,withoutProtocol.indexOf("/"));
	}

	private String concatPaths(String parentPath, String newDirName) {
		String res = parentPath;
		if (!res.endsWith("/"))
			res += "/";
		res += newDirName;
		return res;
	}

	@Override
	public String createFilePath(String parentPath, String newFileName)
			throws Exception {
		if (parentPath.endsWith("/") == false)
			parentPath += "/";
		return parentPath + newFileName;
	}

	@Override
	public List<FileEntry> listFiles(String parentPath) throws Exception {

		ChannelSftp c = init(parentPath);
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

		ChannelSftp c = init(filename);
		try {
			FileEntry fileEntry = new FileEntry();
			String sessionPath = extractSessionPath(filename);
			SftpATTRS attr = c.stat(sessionPath);
			setFromAttrs(fileEntry, attr);
			fileEntry.path = filename;
			fileEntry.displayName = getFilename(sessionPath);
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

		ChannelSftp c = init(path);
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
	
	ChannelSftp init(String filename) throws JSchException, UnsupportedEncodingException {
		jsch = new JSch();
		ConnectionInfo ci = splitStringToConnectionInfo(filename);

		String base_dir = getBaseDir();
		jsch.setKnownHosts(base_dir + "/known_hosts");

		String key_filename = getKeyFileName();
		try{
			createKeyPair(key_filename);
		} catch (Exception ex) {
			System.out.println(ex);
		}

		try {
			jsch.addIdentity(key_filename);
		} catch (java.lang.Exception e)
		{

		}

		Session session = jsch.getSession(ci.username, ci.host, ci.port);
		UserInfo ui = new SftpUserInfo(ci.password,_appContext);
		session.setUserInfo(ui);

		session.setConfig("PreferredAuthentications", "publickey,password");

		session.connect();

		Channel channel = session.openChannel("sftp");
		channel.connect();
		ChannelSftp c = (ChannelSftp) channel;

		logDebug("success: init Sftp");
		return c;

	}

	private String getBaseDir() {
		return _appContext.getFilesDir().getAbsolutePath();
	}

	private String getKeyFileName() {
		return getBaseDir() + "/id_kp2a_rsa";
	}

	public String createKeyPair() throws IOException, JSchException {
		return createKeyPair(getKeyFileName());

	}

	private String createKeyPair(String key_filename) throws JSchException, IOException {
		String public_key_filename = key_filename + ".pub";
		File file = new File(key_filename);
		if (file.exists())
			return public_key_filename;
		int type = KeyPair.RSA;
		KeyPair kpair = KeyPair.genKeyPair(jsch, type, 2048);
		kpair.writePrivateKey(key_filename);

		kpair.writePublicKey(public_key_filename, "generated by Keepass2Android");
		//ret = "Fingerprint: " + kpair.getFingerPrint();
		kpair.dispose();
		return public_key_filename;

	}

	public ConnectionInfo splitStringToConnectionInfo(String filename)
			throws UnsupportedEncodingException {
		ConnectionInfo ci = new ConnectionInfo();
		ci.host = extractUserPwdHost(filename);
		String userPwd = ci.host.substring(0, ci.host.indexOf('@'));
		ci.username = decode(userPwd.substring(0, userPwd.indexOf(":")));
		ci.password = decode(userPwd.substring(userPwd.indexOf(":")+1));
		ci.host = ci.host.substring(ci.host.indexOf('@') + 1);
		ci.port = DEFAULT_SFTP_PORT;
		int portSeparatorIndex = ci.host.indexOf(":");
		if (portSeparatorIndex >= 0)
		{
			ci.port = Integer.parseInt(ci.host.substring(portSeparatorIndex+1));
			ci.host = ci.host.substring(0, portSeparatorIndex);
		}
		ci.localPath = extractSessionPath(filename);
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
			return getProtocolPrefix()+ci.username+"@"+ci.host+ci.localPath;
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

	public String buildFullPath( String host, int port, String localPath, String username, String password) throws UnsupportedEncodingException
	{
		if (port != DEFAULT_SFTP_PORT)
			host += ":"+String.valueOf(port);
		return getProtocolPrefix()+encode(username)+":"+encode(password)+"@"+host+localPath;
		
	}


	@Override
	public void prepareFileUsage(Context appContext, String path) {
		//nothing to do
		
	}
}
