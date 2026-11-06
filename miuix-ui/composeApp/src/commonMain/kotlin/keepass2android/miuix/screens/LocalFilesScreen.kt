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
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import keepass2android.miuix.fs.LocalFileEntry
import keepass2android.miuix.fs.LocalFileSystem
import top.yukonga.miuix.kmp.basic.Button
import top.yukonga.miuix.kmp.basic.ButtonDefaults
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.ExpandMore
import top.yukonga.miuix.kmp.icon.extended.File
import top.yukonga.miuix.kmp.icon.extended.Folder
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.theme.MiuixTheme

@Composable
fun LocalFilesScreen(
    forKeyFile: Boolean,
    storageGranted: Boolean,
    onRequestStorage: () -> Unit,
    onBack: () -> Unit,
    onPickFile: (LocalFileEntry) -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val rootPath = remember { LocalFileSystem.externalStorageRoot() }
    var currentDir by remember { mutableStateOf(rootPath) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = if (forKeyFile) "选择密钥文件" else "选择数据库文件",
                subtitle = if (storageGranted) currentDir else "",
                scrollBehavior = MiuixScrollBehavior(),
                navigationIcon = {
                    IconButton(onClick = {
                        if (storageGranted && currentDir != rootPath) {
                            currentDir = currentDir.substringBeforeLast('/', missingDelimiterValue = rootPath)
                        } else {
                            onBack()
                        }
                    }, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
                    }
                },
            )
        },
    ) { padding ->
        if (!storageGranted) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(top = padding.calculateTopPadding() + 16.dp, start = 16.dp, end = 16.dp),
            ) {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(20.dp)) {
                        Text("需要「所有文件访问」权限", fontSize = 17.sp, fontWeight = FontWeight.SemiBold, color = colors.onSurface)
                        Spacer(Modifier.height(8.dp))
                        Text(
                            "浏览手机上的本地文件需要授予「所有文件访问」权限。点击下方按钮前往系统设置，开启后返回本应用即可自动进入文件列表。",
                            fontSize = 13.sp,
                            color = colors.onSurfaceVariantSummary,
                        )
                        Spacer(Modifier.height(16.dp))
                        Button(
                            onClick = onRequestStorage,
                            modifier = Modifier.fillMaxWidth().height(48.dp),
                            colors = ButtonDefaults.buttonColorsPrimary(),
                        ) {
                            Text("去授权")
                        }
                    }
                }
                Spacer(Modifier.height(12.dp))
                Text(
                    "如果设置页没有打开，请手动前往：系统设置 → 应用管理 → KeePass2Android miuix → 权限 → 存储。",
                    fontSize = 12.sp,
                    color = colors.onSurfaceVariantSummary,
                    modifier = Modifier.padding(horizontal = 8.dp),
                )
            }
        } else {
            val entries = remember(currentDir) {
                LocalFileSystem.list(currentDir).filter { entry ->
                    // 数据库模式只显示文件夹和 .kdbx；密钥文件模式显示所有文件
                    entry.isDirectory || forKeyFile || isDatabaseFile(entry.name)
                }
            }
            if (entries.isEmpty()) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("空文件夹", fontSize = 14.sp, color = colors.onSurfaceVariantSummary)
                }
            } else {
                LazyColumn(
                    modifier = Modifier.fillMaxWidth(),
                    contentPadding = PaddingValues(
                        start = 16.dp,
                        end = 16.dp,
                        top = padding.calculateTopPadding() + 8.dp,
                        bottom = 32.dp,
                    ),
                ) {
                    items(entries, key = { it.path }) { entry ->
                        EntryRow(
                            entry = entry,
                            showLock = !forKeyFile,
                            onClick = {
                                if (entry.isDirectory) {
                                    currentDir = entry.path
                                } else {
                                    onPickFile(entry)
                                }
                            },
                        )
                    }
                }
            }
        }
    }
}

private fun isDatabaseFile(name: String): Boolean = name.lowercase().endsWith(".kdbx")

@Composable
private fun EntryRow(
    entry: LocalFileEntry,
    showLock: Boolean,
    onClick: () -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(bottom = 10.dp),
        onClick = onClick,
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.padding(vertical = 4.dp),
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(
                        if (entry.isDirectory) colors.primaryContainer else colors.secondaryContainer,
                        RoundedCornerShape(11.dp),
                    ),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = when {
                        entry.isDirectory -> MiuixIcons.Folder
                        showLock -> MiuixIcons.Lock
                        else -> MiuixIcons.File
                    },
                    contentDescription = null,
                    tint = if (entry.isDirectory) colors.onPrimaryContainer else colors.onSecondaryContainer,
                    modifier = Modifier.size(21.dp),
                )
            }
            Spacer(Modifier.width(14.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    entry.name,
                    fontSize = 15.sp,
                    fontWeight = FontWeight.Medium,
                    color = colors.onSurface,
                    maxLines = 1,
                )
                Text(
                    if (entry.isDirectory) "文件夹" else humanSize(entry.size),
                    fontSize = 12.sp,
                    color = colors.onSurfaceVariantSummary,
                )
            }
            if (entry.isDirectory) {
                Icon(MiuixIcons.ExpandMore, null, tint = colors.onSurfaceVariantSummary, modifier = Modifier.size(18.dp))
            }
        }
    }
}

private fun humanSize(bytes: Long): String = when {
    bytes >= 1 shl 20 -> "%.1f MB".format(bytes / 1048576.0)
    bytes >= 1 shl 10 -> "%.1f KB".format(bytes / 1024.0)
    else -> "$bytes B"
}
