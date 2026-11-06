package keepass2android.miuix.app

import android.content.Intent
import android.os.Bundle
import android.os.Environment
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
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
import java.io.File
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
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
import top.yukonga.miuix.kmp.theme.darkColorScheme
import top.yukonga.miuix.kmp.theme.lightColorScheme

class FilePickerActivity : ComponentActivity() {

    companion object {
        const val EXTRA_MODE = "mode"
        const val MODE_DATABASE = "database"
        const val MODE_KEY_FILE = "keyfile"
        private val DATABASE_EXTENSIONS = listOf(".kdbx")
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val mode = intent.getStringExtra(EXTRA_MODE) ?: MODE_DATABASE
        val root = Environment.getExternalStorageDirectory()
        if (!root.exists()) {
            android.widget.Toast.makeText(this, "未找到外部存储", android.widget.Toast.LENGTH_SHORT).show()
            finish()
            return
        }

        setContent {
            MiuixTheme(
                colors = if (isSystemInDarkTheme()) darkColorScheme() else lightColorScheme(),
            ) {
                FileBrowserUi(mode = mode, rootPath = root.absolutePath)
            }
        }
    }

    @Composable
    private fun FileBrowserUi(mode: String, rootPath: String) {
        val colors = MiuixTheme.colorScheme
        var currentDir by remember { mutableStateOf(File(rootPath)) }
        var revision by remember { mutableStateOf(0) }
        val entries = remember(currentDir.path, revision) {
            listEntries(currentDir, mode)
        }

        fun finishWith(file: File) {
            setResult(
                RESULT_OK,
                Intent()
                    .putExtra("name", file.name)
                    .putExtra("path", file.absolutePath)
                    .putExtra("size", file.length()),
            )
            finish()
        }

        Scaffold(
            topBar = {
                TopAppBar(
                    title = if (currentDir.absolutePath == rootPath) "选择文件" else currentDir.name,
                    subtitle = currentDir.absolutePath,
                    navigationIcon = {
                        IconButton(onClick = {
                            if (currentDir.absolutePath != rootPath) {
                                currentDir = currentDir.parentFile ?: File(rootPath)
                            } else {
                                finish()
                            }
                        }, modifier = Modifier.padding(start = 12.dp)) {
                            Icon(MiuixIcons.Back, "返回")
                        }
                    },
                )
            },
        ) { padding ->
            Column(modifier = Modifier.fillMaxSize()) {
                Spacer(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(padding.calculateTopPadding()),
                )
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
                            top = 8.dp,
                            bottom = 32.dp,
                        ),
                    ) {
                        items(entries) { file ->
                            EntryRow(
                                file = file,
                                isDatabase = mode == MODE_DATABASE,
                                onClick = {
                                    if (file.isDirectory) {
                                        currentDir = file
                                        revision++
                                    } else {
                                        finishWith(file)
                                    }
                                },
                            )
                        }
                    }
                }
            }
        }
    }

    @Composable
    private fun EntryRow(
        file: File,
        isDatabase: Boolean,
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
                val iconTint = when {
                    file.isDirectory -> colors.onPrimaryContainer
                    else -> colors.onSecondaryContainer
                }
                val iconBg = when {
                    file.isDirectory -> colors.primaryContainer
                    else -> colors.secondaryContainer
                }
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(iconBg, RoundedCornerShape(11.dp)),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(
                        imageVector = when {
                            file.isDirectory -> MiuixIcons.Folder
                            isDatabase -> MiuixIcons.Lock
                            else -> MiuixIcons.File
                        },
                        contentDescription = null,
                        tint = iconTint,
                        modifier = Modifier.size(21.dp),
                    )
                }
                Spacer(Modifier.width(14.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        file.name,
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Medium,
                        color = colors.onSurface,
                        maxLines = 1,
                    )
                    if (file.isDirectory) {
                        Text("文件夹", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                    } else {
                        Text(humanSize(file.length()), fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                    }
                }
                if (file.isDirectory) {
                    Icon(
                        MiuixIcons.ExpandMore,
                        null,
                        tint = colors.onSurfaceVariantSummary,
                        modifier = Modifier.size(18.dp),
                    )
                }
            }
        }
    }

    private fun listEntries(dir: File, mode: String): List<File> {
        val children = dir.listFiles() ?: return emptyList()
        return children
            .filter { !it.name.startsWith(".") }
            .filter { it.isDirectory || matchesFilter(it, mode) }
            .sortedWith(
                compareByDescending<File> { it.isDirectory }
                    .thenBy { it.name.lowercase() },
            )
    }

    private fun matchesFilter(file: File, mode: String): Boolean = when (mode) {
        MODE_DATABASE -> DATABASE_EXTENSIONS.any { file.name.lowercase().endsWith(it) }
        else -> true
    }

    private fun humanSize(bytes: Long): String = when {
        bytes >= 1 shl 20 -> "%.1f MB".format(bytes / 1048576.0)
        bytes >= 1 shl 10 -> "%.1f KB".format(bytes / 1024.0)
        else -> "$bytes B"
    }
}
