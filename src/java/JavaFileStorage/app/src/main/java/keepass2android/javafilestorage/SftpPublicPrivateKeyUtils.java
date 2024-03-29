package keepass2android.javafilestorage;

import android.util.Pair;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileWriter;
import java.io.IOException;
import java.io.StringReader;
import java.util.regex.Pattern;

import androidx.annotation.Nullable;

import com.jcraft.jsch.JSch;
import com.jcraft.jsch.JSchException;
import com.jcraft.jsch.KeyPair;

class SftpPublicPrivateKeyUtils {

    private enum Validity {
        NOT_ATTEMPTED, VALID, NOT_VALID;
    }

    private static final String SFTP_CUSTOM_KEY_DIRNAME = "user_keys";

    private static final String KP2A_PRIVATE_KEY_FILENAME = "id_kp2a_rsa";

    private final File appBaseDir;

    /**
     * Do NOT access this variable directly! Use {@link #baseDir()} instead.
     */
    private final File customKeyBaseDir;
    private volatile Validity validDir = Validity.NOT_ATTEMPTED;

    SftpPublicPrivateKeyUtils(String appBaseDir) {
        // Assume app base directory exists already
        this.appBaseDir = new File(appBaseDir);

        // Intentionally skipping existence/creation checking in constructor
        // See baseDir()
        this.customKeyBaseDir = new File(appBaseDir, SFTP_CUSTOM_KEY_DIRNAME);
    }

    private Pair<File, Boolean> baseDir() {
        if (validDir == Validity.NOT_ATTEMPTED) {
            synchronized (this) {
                if (!customKeyBaseDir.exists()) {
                    customKeyBaseDir.mkdirs();
                }
                if (customKeyBaseDir.exists() && customKeyBaseDir.isDirectory()) {
                    validDir = Validity.VALID;
                } else {
                    validDir = Validity.NOT_VALID;
                }
            }
        }
        return new Pair<>(customKeyBaseDir, validDir == Validity.VALID);
    }

    boolean deleteCustomKey(String keyName) throws FileNotFoundException {
        File f = getCustomKeyFile(keyName);
        return f.isFile() && f.delete();
    }

    String[] getCustomKeyNames() {
        Pair<File, Boolean> base = baseDir();
        if (!base.second) {
            // Log it?
            return new String[]{};
        }
        return base.first.list();
    }

    void savePrivateKeyContent(String keyName, String keyContent) throws IOException, Exception {
        keyContent = PrivateKeyValidator.ensureValidContent(keyContent);

        File f = getCustomKeyFile(keyName);
        try (BufferedWriter w = new BufferedWriter(new FileWriter(f))) {
            w.write(keyContent);
        }
    }

    String getCustomKeyFilePath(String customKeyName) throws FileNotFoundException {
        return getCustomKeyFile(customKeyName).getAbsolutePath();
    }

    String resolveKeyFilePath(JSch jschInst, @Nullable String customKeyName) {
        // Custom private key configured
        if (customKeyName != null) {
            try {
                return getCustomKeyFilePath(customKeyName);
            } catch (FileNotFoundException e) {
                System.out.println(e);
            }
        }
        // Use KP2A's public/private key
        String keyFilePath = getAppKeyFileName();
        try{
            createKeyPair(jschInst, keyFilePath);
        } catch (Exception ex) {
            System.out.println(ex);
        }
        return keyFilePath;
    }

    String createKeyPair(JSch jschInst) throws IOException, JSchException {
        return createKeyPair(jschInst, getAppKeyFileName());
    }

    /**
     * Exposed for testing purposes only
     * @param keyName
     * @return
     */
    String getSanitizedCustomKeyName(String keyName) {
        return PrivateKeyValidator.sanitizeKeyAsFilename(keyName);
    }

    /**
     * Exposed for testing purposes only.
     * @param keyContent
     * @return
     * @throws Exception
     */
    String getValidatedCustomKeyContent(String keyContent) throws Exception {
        return PrivateKeyValidator.ensureValidContent(keyContent);
    }

    private String createKeyPair(JSch jschInst, String key_filename) throws JSchException, IOException {
        String public_key_filename = key_filename + ".pub";
        File file = new File(key_filename);
        if (file.exists())
            return public_key_filename;
        int type = KeyPair.RSA;
        KeyPair kpair = KeyPair.genKeyPair(jschInst, type, 4096);
        kpair.writePrivateKey(key_filename);

        kpair.writePublicKey(public_key_filename, "generated by Keepass2Android");
        //ret = "Fingerprint: " + kpair.getFingerPrint();
        kpair.dispose();
        return public_key_filename;
    }

    private String getAppKeyFileName() {
        return new File(appBaseDir, KP2A_PRIVATE_KEY_FILENAME).getAbsolutePath();
    }

    private File getCustomKeyFile(String customKeyName) throws FileNotFoundException {
        Pair<File, Boolean> base = baseDir();
        if (!base.second) {
            throw new FileNotFoundException("Custom key directory");
        }

        String keyFileName = PrivateKeyValidator.sanitizeKeyAsFilename(customKeyName);
        if (!keyFileName.isEmpty()) {
            File keyFile = new File(base.first, keyFileName);
            // Protect against bad actors trying to navigate away from the base directory.
            // This is probably overkill, given sanitizeKeyAsFilename(...) but better safe than sorry.
            if (base.first.equals(keyFile.getParentFile())) {
                return keyFile;
            }
        }
        // The key was sanitized to nothing, or the parent check above failed.
        throw new FileNotFoundException("Malformed key name");
    }


    private static class PrivateKeyValidator {
        private static final Pattern CONTENT_FIRST_LINE = Pattern.compile("^-+BEGIN\\s[^\\s]+\\sPRIVATE\\sKEY-+$");
        private static final Pattern CONTENT_LAST_LINE = Pattern.compile("^-+END\\s[^\\s]+\\sPRIVATE\\sKEY-+$");

        /**
         * Key-to-filename sanitizer solution sourced from:
         *  <a href="https://www.b4x.com/android/forum/threads/sanitize-filename.82558/" />
         */
        private static final Pattern KEY_SANITIZER = Pattern.compile("([^\\p{L}\\s\\d\\-_~,;:\\[\\]\\(\\).'])",
                Pattern.CASE_INSENSITIVE);

        static String sanitizeKeyAsFilename(String key) {
            return KEY_SANITIZER.matcher(key.trim()).replaceAll("");
        }

        static String ensureValidContent(String content) throws Exception {
            content = content.trim();

            boolean isValid = true;
            try (BufferedReader r = new BufferedReader(new StringReader(content))) {
                boolean validFirst = false;
                String line;
                String last = null;
                while ((line = r.readLine()) != null) {
                    if (!validFirst) {
                        if (CONTENT_FIRST_LINE.matcher(line).matches()) {
                            validFirst = true;
                        } else {
                            isValid = false;
                            break;
                        }
                    }
                    last = line;
                }
                if (!isValid || last == null || !CONTENT_LAST_LINE.matcher(last).matches()) {
                    throw new RuntimeException("Malformed private key content");
                }
            } catch (Exception e) {
                android.util.Log.d(SftpStorage.class.getName(), "Invalid key content", e);
                throw e;
            }

            return content;
        }
    }
}
