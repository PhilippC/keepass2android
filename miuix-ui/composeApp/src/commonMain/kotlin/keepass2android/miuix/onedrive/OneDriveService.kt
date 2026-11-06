package keepass2android.miuix.onedrive

/** 设备码登录流程状态（保存在服务单例中，跨界面/切出应用不丢失） */
sealed interface OneDriveSignInState {
    data object Idle : OneDriveSignInState
    data class Pending(val verifyUrl: String, val userCode: String) : OneDriveSignInState
    data object Success : OneDriveSignInState
    data class Failed(val message: String) : OneDriveSignInState
}

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

    /** 当前登录流程状态（可观察） */
    val signInState: OneDriveSignInState

    /** 初始化：注入 Client ID 与缓存目录，并尝试恢复会话 */
    fun init(cacheDirPath: String, clientId: String)

    /** 开始设备码登录（后台轮询，与本界面生命周期无关） */
    fun startSignIn()

    /** 取消登录 */
    fun cancelSignIn()

    /** 确认登录成功，状态复位 */
    fun ackSignIn()

    /** 退出登录并清除本地会话 */
    fun signOut()

    /** 登录后获取账号显示名 */
    suspend fun accountName(): String

    /** 列出 OneDrive 根目录（folderId 为 null）或指定文件夹的内容 */
    suspend fun listChildren(folderId: String?): List<OneDriveItem>

    /** 下载指定文件到 targetDir */
    suspend fun downloadItem(item: OneDriveItem, targetDir: String, fallbackName: String): OneDriveFile
}
