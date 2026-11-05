package keepass2android.miuix.onedrive

expect class OneDriveItem(id: String, name: String, isFolder: Boolean, sizeBytes: Long) {
    val id: String
    val name: String
    val isFolder: Boolean
    val sizeBytes: Long
}

expect object OneDriveService {

    /** 是否已配置 Azure 应用 Client ID */
    val isConfigured: Boolean

    /** 是否已登录（本地存在会话） */
    val isSignedIn: Boolean

    /** 初始化：注入 Client ID 与缓存目录，并尝试恢复会话 */
    fun init(cacheDirPath: String, clientId: String)

    /** 设备码登录：先回调 onCode(验证网址, 用户代码)，然后阻塞轮询直到授权完成/失败 */
    suspend fun signIn(onCode: (verifyUrl: String, userCode: String) -> Unit, isCancelled: () -> Boolean)

    /** 退出登录并清除本地会话 */
    fun signOut()

    /** 登录后获取账号显示名 */
    suspend fun accountName(): String

    /** 列出 OneDrive 根目录（folderId 为 null）或指定文件夹的内容 */
    suspend fun listChildren(folderId: String?): List<OneDriveItem>

    /** 下载指定文件到 targetDir */
    suspend fun downloadItem(item: OneDriveItem, targetDir: String, fallbackName: String): OneDriveFile
}
