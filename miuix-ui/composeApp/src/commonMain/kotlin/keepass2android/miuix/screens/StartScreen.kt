package keepass2android.miuix.screens

import androidx.compose.animation.core.Animatable
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectHorizontalDragGestures
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.launch
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
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.CloudFill
import top.yukonga.miuix.kmp.icon.extended.Delete
import top.yukonga.miuix.kmp.icon.extended.File
import top.yukonga.miuix.kmp.icon.extended.Folder
import top.yukonga.miuix.kmp.icon.extended.Lock
import top.yukonga.miuix.kmp.icon.extended.Settings
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.DemoData
import keepass2android.miuix.data.RecentDatabase

@Composable
fun StartScreen(
    loadRecents: () -> List<RecentDatabase> = { DemoData.recentDatabases },
    saveRecents: (List<RecentDatabase>) -> Unit = {},
    onPickDatabase: () -> Unit,
    onOpenDatabase: () -> Unit,
    onCreateDatabase: () -> Unit = {},
    onSettings: () -> Unit,
) {
    val scrollBehavior = MiuixScrollBehavior()
    val colors = MiuixTheme.colorScheme
    val recents = remember {
        mutableStateListOf<RecentDatabase>().apply { addAll(loadRecents()) }
    }
    var pendingDelete by remember { mutableStateOf<RecentDatabase?>(null) }

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
        Box(modifier = Modifier.fillMaxSize()) {
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
                        onClick = onOpenDatabase,
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
                                    "选择存储位置：本地文件或 OneDrive",
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
                            .padding(bottom = 16.dp),
                        onClick = onCreateDatabase,
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
                items(recents.size) { index ->
                    val db = recents[index]
                    RecentCard(
                        db = db,
                        onClick = onOpenDatabase,
                        onSwipeDelete = { pendingDelete = db },
                    )
                }
            }

            if (pendingDelete != null) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(Color.Black.copy(alpha = 0.35f))
                        .clickable { pendingDelete = null },
                    contentAlignment = Alignment.Center,
                ) {
                    Card(
                        modifier = Modifier
                            .padding(horizontal = 32.dp)
                            .clickable(enabled = false) {},
                    ) {
                        Column(modifier = Modifier.padding(20.dp)) {
                            Text("删除记录", fontSize = 17.sp, fontWeight = FontWeight.SemiBold, color = colors.onSurface)
                            Spacer(Modifier.height(8.dp))
                            Text(
                                "确定删除「${pendingDelete?.name}」的最近使用记录吗？",
                                fontSize = 13.sp,
                                color = colors.onSurfaceVariantSummary,
                            )
                            Spacer(Modifier.height(16.dp))
                            Row(
                                horizontalArrangement = Arrangement.End,
                                modifier = Modifier.fillMaxWidth(),
                            ) {
                                TextButton(text = "取消", onClick = { pendingDelete = null })
                                Spacer(Modifier.width(8.dp))
                                TextButton(
                                    text = "删除",
                                    onClick = {
                                        pendingDelete?.let { recents.remove(it) }
                                        saveRecents(recents.toList())
                                        pendingDelete = null
                                    },
                                    colors = top.yukonga.miuix.kmp.basic.ButtonDefaults.textButtonColors(
                                        color = colors.secondaryContainer,
                                        textColor = colors.error,
                                    ),
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun RecentCard(
    db: RecentDatabase,
    onClick: () -> Unit,
    onSwipeDelete: () -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val density = LocalDensity.current
    val scope = rememberCoroutineScope()
    val offsetX = remember { Animatable(0f) }
    val thresholdPx = with(density) { 88.dp.toPx() }

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(bottom = 12.dp),
    ) {
        // 左划 → 右侧删除区
        Box(
            modifier = Modifier
                .matchParentSize()
                .background(colors.error, RoundedCornerShape(16.dp)),
            contentAlignment = Alignment.CenterEnd,
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.padding(end = 24.dp),
            ) {
                Text("删除", color = colors.onPrimary)
                Spacer(Modifier.width(6.dp))
                Icon(MiuixIcons.Delete, null, tint = colors.onPrimary, modifier = Modifier.size(20.dp))
            }
        }
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .graphicsLayer { translationX = offsetX.value }
                .pointerInput(Unit) {
                    detectHorizontalDragGestures(
                        onHorizontalDrag = { change, dragAmount ->
                            change.consume()
                            scope.launch {
                                offsetX.snapTo((offsetX.value + dragAmount).coerceIn(-thresholdPx * 1.8f, 0f))
                            }
                        },
                        onDragEnd = {
                            if (offsetX.value <= -thresholdPx) {
                                scope.launch { offsetX.animateTo(0f) }
                                onSwipeDelete()
                            } else {
                                scope.launch { offsetX.animateTo(0f) }
                            }
                        },
                        onDragCancel = {
                            scope.launch { offsetX.animateTo(0f) }
                        },
                    )
                },
            onClick = onClick,
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
                    onClick = onClick,
                    colors = top.yukonga.miuix.kmp.basic.ButtonDefaults.textButtonColors(
                        color = colors.primaryContainer,
                        textColor = colors.onPrimaryContainer,
                    ),
                )
            }
        }
    }
}

@Composable
fun OpenDatabaseChooserScreen(
    onBack: () -> Unit,
    onPickDatabase: () -> Unit,
    onOpenLocalFiles: () -> Unit,
    onOpenOneDrive: () -> Unit,
) {
    val colors = MiuixTheme.colorScheme

    Scaffold(
        topBar = {
            TopAppBar(
                title = "选择存储类型",
                scrollBehavior = MiuixScrollBehavior(),
                navigationIcon = {
                    IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
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
                            Text("系统文件选择器", color = colors.onSurface)
                            Text("浏览本地 .kdbx 与密钥文件", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
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
                            Text("OneDrive", color = colors.onSurface)
                            Text("登录账号浏览云端的 .kdbx 与密钥文件", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                        }
                    }
                }
            }

            item {
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(bottom = 12.dp),
                    onClick = onOpenLocalFiles,
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
                            Icon(MiuixIcons.Folder, null, tint = colors.onPrimaryContainer, modifier = Modifier.size(22.dp))
                        }
                        Column(modifier = Modifier.padding(start = 14.dp)) {
                            Text("本地文件浏览器", color = colors.onSurface)
                            Text("内置浏览本地存储（系统选择器不可用时的备用方式）", fontSize = 12.sp, color = colors.onSurfaceVariantSummary)
                        }
                    }
                }
            }
        }
    }
}
