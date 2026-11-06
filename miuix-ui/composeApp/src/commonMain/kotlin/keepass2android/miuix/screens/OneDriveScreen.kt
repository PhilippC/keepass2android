package keepass2android.miuix.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.CloudFill
import top.yukonga.miuix.kmp.icon.extended.Download
import top.yukonga.miuix.kmp.icon.extended.File
import top.yukonga.miuix.kmp.icon.extended.Folder
import top.yukonga.miuix.kmp.icon.extended.Refresh
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.RecentDatabase
import keepass2android.miuix.onedrive.OneDriveSignInState
import keepass2android.miuix.onedrive.OneDriveItem
import keepass2android.miuix.onedrive.OneDriveService

private fun humanSize(bytes: Long): String = when {
    bytes >= 1 shl 20 -> "%.1f MB".format(bytes / 1048576.0)
    bytes >= 1 shl 10 -> "%.1f KB".format(bytes / 1024.0)
    else -> "$bytes B"
}

@Composable
fun OneDriveScreen(
    cacheDirPath: String,
    clientId: String,
    onOpenUrl: (String) -> Unit,
    onBack: () -> Unit,
    onFetched: (database: RecentDatabase, keyFileName: String?, keyFilePath: String?) -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val scope = rememberCoroutineScope()

    // 会话状态
    var sessionReady by remember { mutableStateOf(false) }
    var signedIn by remember { mutableStateOf(false) }
    var account by remember { mutableStateOf("") }

    // 登录状态（流程状态保存在 OneDriveService 中，切出应用/切换页面不丢失）
    var codeCopied by remember { mutableStateOf(false) }
    val clipboardManager = LocalClipboardManager.current

    LaunchedEffect(codeCopied) {
        if (codeCopied) {
            delay(2500)
            codeCopied = false
        }
    }

    // 文件浏览状态
    val folderStack = remember { mutableStateListOf<OneDriveItem>() }
    var items by remember { mutableStateOf<List<OneDriveItem>>(emptyList()) }
    var browsing by remember { mutableStateOf(false) }
    var browseError by remember { mutableStateOf<String?>(null) }
    var status by remember { mutableStateOf<String?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var keyFileName by remember { mutableStateOf<String?>(null) }
    var keyPath by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(clientId) {
        OneDriveService.init(cacheDirPath, clientId)
        if (OneDriveService.isConfigured && OneDriveService.isSignedIn) {
            signedIn = true
            account = runCatching { OneDriveService.accountName() }.getOrDefault("OneDrive 账号")
        }
        sessionReady = true
    }

    fun refreshFolder() {
        if (!signedIn) return
        scope.launch {
            browsing = true
            browseError = null
            try {
                items = OneDriveService.listChildren(folderStack.lastOrNull()?.id)
            } catch (e: Exception) {
                browseError = e.message ?: "加载失败"
            }
            browsing = false
        }
    }

    fun enterFolder(item: OneDriveItem) {
        folderStack.add(item)
        refreshFolder()
    }

    fun upFolder() {
        if (folderStack.isNotEmpty()) {
            folderStack.removeAt(folderStack.lastIndex)
            refreshFolder()
        }
    }

    fun signOut() {
        OneDriveService.signOut()
        signedIn = false
        account = ""
        items = emptyList()
        folderStack.clear()
    }

    LaunchedEffect(OneDriveService.signInState) {
        if (OneDriveService.signInState is OneDriveSignInState.Success && !signedIn) {
            signedIn = true
            account = runCatching { OneDriveService.accountName() }.getOrDefault("OneDrive 账号")
            refreshFolder()
            OneDriveService.ackSignIn()
        }
    }

    fun downloadAsDatabase(item: OneDriveItem) {
        scope.launch {
            status = "正在下载 ${item.name}…"
            error = null
            try {
                val file = OneDriveService.downloadItem(item, cacheDirPath, "database.kdbx")
                status = null
                onFetched(RecentDatabase(file.name, "OneDrive · ${humanSize(file.sizeBytes)}", file.path), keyFileName, keyPath)
            } catch (e: Exception) {
                status = null
                error = e.message ?: "下载失败"
            }
        }
    }

    fun downloadAsKeyFile(item: OneDriveItem) {
        scope.launch {
            status = "正在下载密钥文件 ${item.name}…"
            error = null
            try {
                val file = OneDriveService.downloadItem(item, cacheDirPath, "keyfile.keyx")
                status = null
                keyFileName = file.name
                keyPath = file.path
            } catch (e: Exception) {
                status = null
                error = e.message ?: "下载失败"
            }
        }
    }

    fun onItemClick(item: OneDriveItem) {
        if (item.isFolder) {
            enterFolder(item)
            return
        }
        val name = item.name.lowercase()
        when {
            name.endsWith(".kdbx") -> downloadAsDatabase(item)
            name.endsWith(".keyx") || name.endsWith(".key") -> downloadAsKeyFile(item)
            else -> error = "仅支持 .kdbx（数据库）与 .keyx / .key（密钥文件）"
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = "OneDrive",
                navigationIcon = {
                    IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
                    }
                },
                actions = {
                    if (signedIn) {
                        IconButton(onClick = ::refreshFolder) {
                            Icon(MiuixIcons.Refresh, "刷新")
                        }
                    }
                },
            )
        },
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxWidth(),
            contentPadding = PaddingValues(
                start = 16.dp,
                end = 16.dp,
                top = padding.calculateTopPadding() + 8.dp,
                bottom = padding.calculateBottomPadding() + 32.dp,
            ),
        ) {
            if (!sessionReady) {
                item {
                    Text("正在加载…", color = colors.onSurfaceVariantSummary, modifier = Modifier.padding(16.dp))
                }
            } else if (!OneDriveService.isConfigured) {
                item {
                    Card {
                        Column(modifier = Modifier.padding(14.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(MiuixIcons.CloudFill, null, tint = colors.primary, modifier = Modifier.size(24.dp))
                                Spacer(Modifier.width(12.dp))
                                Text("需要配置 Client ID", fontWeight = FontWeight.SemiBold, color = colors.onSurface)
                            }
                            Spacer(Modifier.height(8.dp))
                            Text(
                                "登录 OneDrive 需要一个 Azure 应用 Client ID：\n" +
                                    "1. 打开 portal.azure.com → 应用注册 → 新注册\n" +
                                    "2. 受支持的账户类型选「任何组织目录和个人账户」\n" +
                                    "3. 身份验证 → 高级设置 → 允许公共客户端流 = 是\n" +
                                    "4. 把应用程序(客户端) ID 填入 AndroidManifest 的 onedrive_client_id（桌面端设环境变量 ONEDRIVE_CLIENT_ID）",
                                fontSize = 13.sp,
                                color = colors.onSurfaceVariantSummary,
                            )
                        }
                    }
                }
            } else if (!signedIn) {
                item {
                    when (val signIn = OneDriveService.signInState) {
                        is OneDriveSignInState.Pending -> {
                        Card {
                            Column(modifier = Modifier.padding(14.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                                Text("第一步：点击下方按钮打开授权页面", fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                                Spacer(Modifier.height(10.dp))
                                Button(
                                    onClick = { onOpenUrl(signIn.verifyUrl) },
                                    modifier = Modifier.fillMaxWidth(),
                                    colors = ButtonDefaults.buttonColorsPrimary(),
                                ) {
                                    Text("打开授权页面")
                                }
                                Spacer(Modifier.height(14.dp))
                                Text("第二步：在页面中输入此代码", fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                                Spacer(Modifier.height(6.dp))
                                Text(signIn.userCode, fontSize = 28.sp, fontWeight = FontWeight.Bold, color = colors.onSurface)
                                TextButton(
                                    text = if (codeCopied) "已复制" else "复制代码",
                                    onClick = {
                                        clipboardManager.setText(AnnotatedString(signIn.userCode))
                                        codeCopied = true
                                    },
                                )
                                Spacer(Modifier.height(10.dp))
                                Text("等待授权中…", fontSize = 13.sp, color = colors.onSurfaceVariantSummary)
                                Spacer(Modifier.height(12.dp))
                                TextButton(text = "取消", onClick = { OneDriveService.cancelSignIn() })
                            }
                        }
                        }
                        else -> {
                        Card {
                            Column(modifier = Modifier.padding(14.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                                Icon(MiuixIcons.CloudFill, null, tint = colors.primary, modifier = Modifier.size(40.dp))
                                Spacer(Modifier.height(10.dp))
                                Text("登录 OneDrive 账号", fontWeight = FontWeight.SemiBold, color = colors.onSurface)
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    "使用微软设备码登录，无需输入账号密码到本应用",
                                    fontSize = 13.sp,
                                    color = colors.onSurfaceVariantSummary,
                                )
                                if (signIn is OneDriveSignInState.Failed) {
                                    Spacer(Modifier.height(8.dp))
                                    Text(signIn.message, fontSize = 13.sp, color = colors.error)
                                }
                                Spacer(Modifier.height(14.dp))
                                Button(
                                    onClick = { OneDriveService.startSignIn() },
                                    modifier = Modifier.fillMaxWidth(),
                                    colors = ButtonDefaults.buttonColorsPrimary(),
                                ) {
                                    Text("登录 OneDrive")
                                }
                            }
                        }
                        }
                    }
                }
            } else {
                item {
                    Card(modifier = Modifier.padding(bottom = 12.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 4.dp)) {
                            Box(
                                modifier = Modifier
                                    .size(40.dp)
                                    .background(colors.primaryContainer, RoundedCornerShape(12.dp)),
                                contentAlignment = Alignment.Center,
                            ) {
                                Icon(MiuixIcons.CloudFill, null, tint = colors.primary, modifier = Modifier.size(22.dp))
                            }
                            Column(modifier = Modifier.padding(start = 12.dp).weight(1f)) {
                                Text(account, color = colors.onSurface)
                                Text("已连接 OneDrive", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                            }
                            TextButton(text = "退出登录", onClick = ::signOut)
                        }
                    }
                    if (folderStack.isNotEmpty()) {
                        TextButton(text = "← 返回上级", onClick = ::upFolder)
                    }
                    Text(
                        "文件列表" + if (folderStack.isNotEmpty()) " · ${folderStack.last().name}" else "",
                        fontSize = 13.sp,
                        color = colors.onSurfaceVariantSummary,
                        modifier = Modifier.padding(top = 6.dp, bottom = 8.dp, start = 4.dp),
                    )
                    if (keyFileName != null) {
                        Card(modifier = Modifier.padding(bottom = 8.dp)) {
                            Text(
                                "已选择密钥文件：$keyFileName",
                                fontSize = 13.sp,
                                color = colors.primary,
                                modifier = Modifier.padding(12.dp),
                            )
                        }
                    }
                    status?.let {
                        Text(it, fontSize = 13.sp, color = colors.onSurfaceVariantSummary, modifier = Modifier.padding(bottom = 8.dp, start = 4.dp))
                    }
                    error?.let {
                        Text(it, fontSize = 13.sp, color = colors.error, modifier = Modifier.padding(bottom = 8.dp, start = 4.dp))
                    }
                    if (browsing) {
                        Text("加载中…", fontSize = 13.sp, color = colors.onSurfaceVariantSummary, modifier = Modifier.padding(8.dp))
                    }
                    browseError?.let {
                        Text(it, fontSize = 13.sp, color = colors.error, modifier = Modifier.padding(8.dp))
                    }
                }
                items(items.size) { index ->
                    val item = items[index]
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(bottom = 10.dp),
                        onClick = { onItemClick(item) },
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 2.dp)) {
                            Box(
                                modifier = Modifier
                                    .size(38.dp)
                                    .background(
                                        if (item.isFolder) colors.secondaryContainer else colors.primaryContainer,
                                        RoundedCornerShape(10.dp),
                                    ),
                                contentAlignment = Alignment.Center,
                            ) {
                                Icon(
                                    if (item.isFolder) MiuixIcons.Folder else MiuixIcons.File,
                                    null,
                                    tint = if (item.isFolder) colors.onSecondaryContainer else colors.primary,
                                    modifier = Modifier.size(20.dp),
                                )
                            }
                            Column(modifier = Modifier.padding(start = 12.dp).weight(1f)) {
                                Text(item.name, color = colors.onSurface)
                                if (!item.isFolder) {
                                    Text(humanSize(item.sizeBytes), fontSize = 11.sp, color = colors.onSurfaceVariantSummary)
                                }
                            }
                            if (!item.isFolder) {
                                IconButton(onClick = { onItemClick(item) }) {
                                    Icon(MiuixIcons.Download, "下载", tint = colors.primary)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
