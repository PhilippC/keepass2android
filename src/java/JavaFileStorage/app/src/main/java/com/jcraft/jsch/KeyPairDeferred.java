package com.jcraft.jsch;

import com.jcraft.jsch.jbcrypt.BCrypt;

import java.util.Arrays;

/**
 * A {@link KeyPair} which can only reveal its type and content after it was decrypted using {@link com.jcraft.jsch.KeyPairDeferred#decrypt(byte[])}.
 * This is needed for openssh-v1-private-key format.
 */
class KeyPairDeferred extends KeyPair {

    private KeyPair delegate;

    KeyPairDeferred(JSch jsch) {
        super(jsch);
    }

    @Override
    public boolean decrypt(String _passphrase) {
        return decrypt(Util.str2byte(_passphrase));
    }

    @Override
    public boolean decrypt(byte[] _passphrase) {
        try {
            if (!isEncrypted()) {
                return true;
            }
            if (_passphrase == null) {
                jsch.getInstanceLogger().log(Logger.ERROR, "no passphrase set.");
                return false;
            }

            initCipher(_passphrase);

            byte[] plain = new byte[data.length];
            cipher.update(data, 0, data.length, plain, 0);

            // now we have decrypted key and can determine type
            int type = readOpenSSHKeyv1(plain);

            delegate = getKeyPair(jsch, null, null, null, false, plain, getPublicKeyBlob(), type, VENDOR_OPENSSH_V1, publicKeyComment, cipher, null, null);

            return delegate != null;


        } catch (Exception e) {
            throw new IllegalArgumentException("Could not sucessfully decrypt openssh v1 key", e);
        }

    }

    private void initCipher(byte[] _passphrase) throws Exception {

        // the encrypted private key is here:
        if ("bcrypt".equals(kdfName)) {
            Buffer opts = new Buffer(kdfOptions);

            byte[] keyiv = new byte[48];

            new BCrypt().pbkdf(_passphrase, opts.getString(), opts.getInt(), keyiv);

            Arrays.fill(_passphrase, (byte) 0);
            byte[] key = Arrays.copyOfRange(keyiv, 0, 32);
            byte[] iv = Arrays.copyOfRange(keyiv, 32, 48);
            cipher.init(Cipher.DECRYPT_MODE, key, iv);
        } else {
            throw new IllegalStateException("No support for KDF '" + kdfName + "'.");
        }
    }

    @Override
    void generate(int key_size) throws JSchException {
        throw new UnsupportedOperationException();
    }

    @Override
    byte[] getBegin() {
        return requireDecrypted(delegate).getBegin();
    }

    @Override
    byte[] getEnd() {
        return requireDecrypted(delegate).getEnd();
    }

    @Override
    public int getKeySize() {
        return requireDecrypted(delegate).getKeySize();
    }

    @Override
    public byte[] getSignature(byte[] data) {
        return requireDecrypted(delegate).getSignature(data);
    }

    @Override
    public byte[] getSignature(byte[] data, String alg) {
        return requireDecrypted(delegate).getSignature(data, alg);
    }

    @Override
    public Signature getVerifier() {
        return requireDecrypted(delegate).getVerifier();
    }

    @Override
    public Signature getVerifier(String alg) {
        return requireDecrypted(delegate).getVerifier(alg);
    }

    @Override
    public byte[] forSSHAgent() throws JSchException {
        return requireDecrypted(delegate).forSSHAgent();
    }

    @Override
    byte[] getPrivateKey() {
        return requireDecrypted(delegate).getPrivateKey();
    }

    @Override
    byte[] getKeyTypeName() {
        return requireDecrypted(delegate).getKeyTypeName();
    }

    @Override
    public int getKeyType() {
        return requireDecrypted(delegate).getKeyType();
    }

    @Override
    boolean parse(byte[] data) {
        return requireDecrypted(delegate).parse(data);
    }

    @Override
    public byte[] getPublicKeyBlob() {
        return delegate != null ? delegate.getPublicKeyBlob() : null;
    }

    @Override
    public String getPublicKeyComment() {
        return requireDecrypted(delegate).getPublicKeyComment();
    }

    @Override
    public String getFingerPrint() {
        return requireDecrypted(delegate).getFingerPrint();
    }

    @Override
    public boolean isEncrypted() {
        return delegate != null ? delegate.isEncrypted() : super.isEncrypted();
    }

    private <T> T requireDecrypted(T obj) {
        if (obj == null)
            throw new IllegalStateException("encrypted key has not been decrypted yet.");
        return obj;
    }
}
