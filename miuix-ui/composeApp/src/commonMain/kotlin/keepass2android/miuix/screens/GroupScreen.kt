package keepass2android.miuix.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import top.yukonga.miuix.kmp.basic.BasicComponent
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.FloatingActionButton
import top.yukonga.miuix.kmp.basic.HorizontalDivider
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.InputField
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.SmallTitle
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Add
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.icon.extended.Folder
import top.yukonga.miuix.kmp.icon.extended.Search
import top.yukonga.miuix.kmp.icon.extended.Tune
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.DemoData
import keepass2android.miuix.data.KpEntry

@Composable
private fun EntryIcon(icon: androidx.compose.ui.graphics.vector.ImageVector, modifier: Modifier = Modifier) {
    val colors = MiuixTheme.colorScheme
    Box(
        modifier = modifier
            .size(36.dp)
            .background(colors.primaryContainer, RoundedCornerShape(50)),
        contentAlignment = Alignment.Center,
    ) {
        Icon(icon, null, tint = colors.primary, modifier = Modifier.size(20.dp))
    }
}

@Composable
internal fun EntryRow(entry: KpEntry, onClick: () -> Unit) {
    BasicComponent(
        title = entry.title,
        summary = entry.userName,
        startAction = { EntryIcon(entry.icon) },
        onClick = onClick,
    )
}

@Composable
fun GroupScreen(
    groupId: Int,
    extraEntries: List<KpEntry>,
    onBack: () -> Unit,
    onOpenGroup: (Int) -> Unit,
    onOpenEntry: (Int) -> Unit,
    onAddEntry: () -> Unit,
    onGeneratePassword: () -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val scrollBehavior = MiuixScrollBehavior()
    var query by rememberSaveable { mutableStateOf("") }
    val group = remember(groupId) { DemoData.findGroup(groupId) }
    val subgroups = remember(groupId) { DemoData.subgroupsOf(groupId) }
    val entries = remember(groupId, extraEntries) {
        DemoData.entriesOf(groupId) + extraEntries.filter { it.groupId == groupId }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = group?.title ?: "",
                scrollBehavior = scrollBehavior,
                navigationIcon = {
                    if (groupId != 0) {
                        IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                            Icon(MiuixIcons.Back, "返回")
                        }
                    }
                },
                actions = {
                    IconButton(onClick = onGeneratePassword, modifier = Modifier.padding(end = 12.dp)) {
                        Icon(MiuixIcons.Tune, "密码生成器")
                    }
                },
                bottomContent = {
                    InputField(
                        query = query,
                        onQueryChange = { query = it },
                        onSearch = { },
                        expanded = false,
                        onExpandedChange = { },
                        label = "搜索条目",
                        leadingIcon = { Icon(MiuixIcons.Search, null) },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 10.dp),
                    )
                },
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = onAddEntry, modifier = Modifier.padding(20.dp)) {
                Icon(MiuixIcons.Add, "新建条目")
            }
        },
    ) { padding ->
        val filtered = query.isNotBlank()
        val matchedEntries = if (filtered) {
            entries.filter {
                it.title.contains(query, ignoreCase = true) || it.userName.contains(query, ignoreCase = true)
            }
        } else entries

        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(
                start = 16.dp,
                end = 16.dp,
                top = padding.calculateTopPadding() + 4.dp,
                bottom = padding.calculateBottomPadding() + 88.dp,
            ),
        ) {
            if (!filtered) {
                if (subgroups.isNotEmpty()) {
                    item { SmallTitle("文件夹") }
                    item {
                        Card(modifier = Modifier.fillMaxWidth()) {
                            subgroups.forEachIndexed { index, subgroup ->
                                BasicComponent(
                                    title = subgroup.title,
                                    summary = "${DemoData.entryCountRecursive(subgroup.id)} 个条目",
                                    startAction = {
                                        Box(
                                            modifier = Modifier
                                                .size(36.dp)
                                                .background(colors.secondaryContainer, RoundedCornerShape(50)),
                                            contentAlignment = Alignment.Center,
                                        ) {
                                            Icon(
                                                MiuixIcons.Folder,
                                                null,
                                                tint = colors.onSecondaryContainer,
                                                modifier = Modifier.size(20.dp),
                                            )
                                        }
                                    },
                                    onClick = { onOpenGroup(subgroup.id) },
                                )
                                if (index != subgroups.lastIndex) HorizontalDivider()
                            }
                        }
                    }
                }
                item { SmallTitle("条目") }
            }

            if (matchedEntries.isEmpty() && (filtered || subgroups.isEmpty())) {
                item {
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Column(modifier = Modifier.padding(vertical = 32.dp).fillMaxWidth()) {
                            Text(
                                if (filtered) "没有匹配的条目" else "空分组",
                                color = colors.onSurfaceVariantSummary,
                                modifier = Modifier.align(Alignment.CenterHorizontally),
                            )
                        }
                    }
                }
            } else {
                item {
                    Card(modifier = Modifier.fillMaxWidth()) {
                        matchedEntries.forEachIndexed { index, entry ->
                            EntryRow(entry) { onOpenEntry(entry.id) }
                            if (index != matchedEntries.lastIndex) HorizontalDivider()
                        }
                    }
                }
            }

            item {
                Text(
                    "共 ${subgroups.size} 个文件夹 · ${matchedEntries.size} 个条目",
                    color = colors.onSurfaceVariantSummary,
                    modifier = Modifier.padding(top = 16.dp).fillMaxWidth().padding(horizontal = 16.dp),
                )
            }
        }
    }
}
