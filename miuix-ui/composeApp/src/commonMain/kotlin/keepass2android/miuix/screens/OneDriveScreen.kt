package keepass2android.miuix.screens

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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextField
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.CloudFill
import top.yukonga.miuix.kmp.icon.extended.Download
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.RecentDatabase
import keepass2android.miuix.onedrive.fetchOneDriveSharedFile

private fun humanSize(bytes: Long): String = when {
    bytes >= 1 shl 20 -> "%.1f MB".format(bytes / 1048576.0)
    bytes >= 1 shl 10 -> "%.1f KB".format(bytes / 1024.0)
    else -> "$bytes B"
}

@Composable
fun OneDriveScreen(
    cacheDirPath: String,
    onBack: () -> Unit,
    onFetched: (database: RecentDatabase, keyFileName: String?) -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    var dbLink by remember { mutableStateOf("") }
    var keyLink by remember { mutableStateOf("") }
    var fetching by remember { mutableStateOf(false) }
    var status by remember { mutableStateOf<String?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun fetch() {
        val db = dbLink.trim()
        if (db.isEmpty() || fetching) return
        scope.launch {
            fetching = true
            error = null
            try {
                status = "正在连接 OneDrive…"
                val dbFile = withContext(Dispatchers.Default) {
                    fetchOneDriveSharedFile(db, cacheDirPath, "database.kdbx")
                }
                var keyName: String? = null
                if (keyLink.isNotBlank()) {
                    status = "正在下载密钥文件…"
                    keyName = withContext(Dispatchers.Default) {
                        fetchOneDriveSharedFile(keyLink.trim(), cacheDirPath, "keyfile.keyx").name
                    }
                }
                fetching = false
                status = null
                onFetched(
                    RecentDatabase(dbFile.name, "OneDrive · ${humanSize(dbFile.sizeBytes)}"),
                    keyName,
                )
            } catch (e: Exception) {
                fetching = false
                status = null
                error = e.message ?: "下载失败，请稍后重试"
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = "从 OneDrive 获取",
                navigationIcon = {
                    IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(PaddingValues(start = 16.dp, end = 16.dp, top = padding.calculateTopPadding() + 8.dp)),
        ) {
            Card {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.padding(vertical = 4.dp),
                ) {
                    Icon(MiuixIcons.CloudFill, null, tint = colors.primary, modifier = Modifier.size(26.dp))
                    Spacer(Modifier.width(14.dp))
                    Text(
                        "粘贴 OneDrive 分享链接（支持 1drv.ms 短链），应用会把数据库和密钥文件下载到缓存目录。",
                        fontSize = 13.sp,
                        color = colors.onSurfaceVariantSummary,
                        modifier = Modifier.weight(1f),
                    )
                }
            }

            Spacer(Modifier.height(12.dp))

            Card {
                TextField(
                    value = dbLink,
                    onValueChange = {
                        dbLink = it
                        error = null
                    },
                    label = "数据库分享链接（.kdbx）",
                    leadingIcon = { Icon(MiuixIcons.Download, null, tint = colors.primary) },
                    singleLine = true,
                )
            }

            Spacer(Modifier.height(12.dp))

            Card {
                TextField(
                    value = keyLink,
                    onValueChange = { keyLink = it },
                    label = "密钥文件链接（可选，.keyx / .key）",
                    leadingIcon = { Icon(MiuixIcons.Lock, null, tint = colors.primary) },
                    singleLine = true,
                )
            }

            status?.let {
                Text(
                    it,
                    color = colors.onSurfaceVariantSummary,
                    modifier = Modifier.padding(start = 16.dp, top = 12.dp),
                )
            }
            error?.let {
                Text(
                    it,
                    color = colors.error,
                    modifier = Modifier.padding(start = 16.dp, top = 12.dp),
                )
            }

            Spacer(Modifier.height(20.dp))

            Button(
                onClick = ::fetch,
                enabled = !fetching && dbLink.isNotBlank(),
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.buttonColorsPrimary(),
            ) {
                Text(if (fetching) "获取中…" else "获取并打开")
            }

            Text(
                "原型提示：企业版 SharePoint 分享链接暂不支持；下载完成后进入解锁页（演示数据不解析真实 .kdbx）。",
                fontSize = 12.sp,
                color = colors.onSurfaceVariantSummary,
                modifier = Modifier.padding(top = 14.dp, start = 16.dp),
            )

            Spacer(Modifier.height(padding.calculateBottomPadding() + 32.dp))
        }
    }
}
