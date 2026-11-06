package keepass2android.miuix.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.SmallTitle
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TextButton
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.CloudFill
import top.yukonga.miuix.kmp.icon.extended.File
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.icon.extended.Settings
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.DemoData

@Composable
fun StartScreen(
    onPickDatabase: () -> Unit,
    onOpenDatabase: () -> Unit,
    onOpenOneDrive: () -> Unit,
    onSettings: () -> Unit,
) {
    val scrollBehavior = MiuixScrollBehavior()
    val colors = MiuixTheme.colorScheme

    Scaffold(
        topBar = {
            TopAppBar(
                title = "KeePass2Android",
                subtitle = "安全密码管理",
                scrollBehavior = scrollBehavior,
                actions = {
                    IconButton(onClick = onSettings, modifier = Modifier.padding(end = 12.dp)) {
                        Icon(MiuixIcons.Settings, "设置")
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
            item {
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp, bottom = 12.dp),
                    onClick = onPickDatabase,
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(vertical = 10.dp),
                    ) {
                        Box(
                            modifier = Modifier
                                .size(64.dp)
                                .background(
                                    brush = Brush.linearGradient(
                                        listOf(colors.primary, colors.primaryVariant),
                                    ),
                                    shape = RoundedCornerShape(18.dp),
                                ),
                            contentAlignment = Alignment.Center,
                        ) {
                            Icon(
                                MiuixIcons.Lock,
                                null,
                                tint = colors.onPrimary,
                                modifier = Modifier.size(30.dp),
                            )
                        }
                        Column(modifier = Modifier.padding(start = 16.dp)) {
                            Text("打开数据库", fontSize = 19.sp, fontWeight = FontWeight.SemiBold, color = colors.onSurface)
                            Spacer(Modifier.height(3.dp))
                            Text(
                                "选择本地 .kdbx 文件解锁密码库",
                                fontSize = 13.sp,
                                color = colors.onSurfaceVariantSummary,
                            )
                        }
                    }
                }
            }

            item {
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 12.dp),
                    onClick = onOpenOneDrive,
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(vertical = 4.dp),
                    ) {
                        Box(
                            modifier = Modifier
                                .size(44.dp)
                                .background(colors.primaryContainer, RoundedCornerShape(12.dp)),
                            contentAlignment = Alignment.Center,
                        ) {
                            Icon(MiuixIcons.CloudFill, null, tint = colors.onPrimaryContainer, modifier = Modifier.size(22.dp))
                        }
                        Column(modifier = Modifier.padding(start = 14.dp)) {
                            Text("从 OneDrive 获取", color = colors.onSurface)
                            Text("登录账号浏览云端的 .kdbx 与密钥文件", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                        }
                    }
                }
            }

            item {
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 16.dp),
                    onClick = { /* 原型未实现 */ },
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(vertical = 4.dp),
                    ) {
                        Box(
                            modifier = Modifier
                                .size(44.dp)
                                .background(colors.primaryContainer, RoundedCornerShape(12.dp)),
                            contentAlignment = Alignment.Center,
                        ) {
                            Icon(MiuixIcons.File, null, tint = colors.onPrimaryContainer, modifier = Modifier.size(22.dp))
                        }
                        Column(modifier = Modifier.padding(start = 14.dp)) {
                            Text("创建新数据库", color = colors.onSurface)
                            Text("新建一个空的 .kdbx 密码库", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                        }
                    }
                }
            }

            item { SmallTitle("最近使用") }
            items(DemoData.recentDatabases.size) { index ->
                val db = DemoData.recentDatabases[index]
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 12.dp),
                    onClick = onOpenDatabase,
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(vertical = 2.dp),
                    ) {
                        Box(
                            modifier = Modifier
                                .size(44.dp)
                                .background(colors.secondaryContainer, RoundedCornerShape(12.dp)),
                            contentAlignment = Alignment.Center,
                        ) {
                            Icon(MiuixIcons.File, null, tint = colors.onSecondaryContainer, modifier = Modifier.size(22.dp))
                        }
                        Column(modifier = Modifier.padding(start = 14.dp).weight(1f)) {
                            Text(db.name, color = colors.onSurface)
                            Text(db.path, fontSize = 11.sp, color = colors.onSurfaceVariantSummary)
                        }
                        TextButton(
                            text = "解锁",
                            onClick = onOpenDatabase,
                            colors = top.yukonga.miuix.kmp.basic.ButtonDefaults.textButtonColors(
                                color = colors.primaryContainer,
                                textColor = colors.onPrimaryContainer,
                            ),
                        )
                    }
                }
            }
        }
    }
}
