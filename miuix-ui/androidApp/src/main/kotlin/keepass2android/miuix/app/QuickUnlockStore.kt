package keepass2android.miuix.app

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * 快速解锁凭据存储：主密码 + 密钥路径以 Android Keystore AES-GCM 加密后存入本地。
 * 解密仅在 BiometricPrompt 验证通过后由调用方触发。
 */
object QuickUnlockStore {

    private const val PREFS = "quick_unlock"
    private const val KEY_ALIAS = "kp2a_quick_unlock_key"
    private const val GCM_IV_BYTES = 12

    fun has(context: Context, dbPath: String): Boolean =
        prefs(context).contains(dbPath)

    fun save(context: Context, dbPath: String, password: String, keyPath: String?) {
        try {
            val secret = "$password\u0000${keyPath ?: ""}".toByteArray(Charsets.UTF_8)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply {
                init(Cipher.ENCRYPT_MODE, secretKey())
            }
            val encrypted = cipher.doFinal(secret)
            val blob = Base64.encodeToString(cipher.iv + encrypted, Base64.NO_WRAP)
            prefs(context).edit().putString(dbPath, blob).apply()
        } catch (_: Exception) {
            // 加密失败时静默放弃快速解锁能力
        }
    }

    /** 返回 (主密码, 密钥路径)；失败返回 null */
    fun load(context: Context, dbPath: String): Pair<String, String?>? {
        return try {
            val blob = prefs(context).getString(dbPath, null) ?: return null
            val data = Base64.decode(blob, Base64.NO_WRAP)
            val iv = data.copyOfRange(0, GCM_IV_BYTES)
            val encrypted = data.copyOfRange(GCM_IV_BYTES, data.size)
            val cipher = Cipher.getInstance("AES/GCM/NoPadding").apply {
                init(Cipher.DECRYPT_MODE, secretKey(), GCMParameterSpec(128, iv))
            }
            val plain = String(cipher.doFinal(encrypted), Charsets.UTF_8)
            val separator = plain.indexOf('\u0000')
            if (separator < 0) return null
            plain.substring(0, separator) to plain.substring(separator + 1).ifEmpty { null }
        } catch (_: Exception) {
            null
        }
    }

    private fun prefs(context: Context) =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    private fun secretKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance("AES", "AndroidKeyStore").apply {
            init(
                KeyGenParameterSpec.Builder(
                    KEY_ALIAS,
                    KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                )
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .setKeySize(256)
                    .build(),
            )
        }.generateKey()
    }
}
