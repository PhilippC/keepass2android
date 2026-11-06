package keepass2android.miuix.screens

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import top.yukonga.miuix.kmp.basic.Card
import top.yukonga.miuix.kmp.basic.HorizontalDivider
import top.yukonga.miuix.kmp.basic.Icon
import top.yukonga.miuix.kmp.basic.IconButton
import top.yukonga.miuix.kmp.basic.MiuixScrollBehavior
import top.yukonga.miuix.kmp.basic.Scaffold
import top.yukonga.miuix.kmp.basic.Text
import top.yukonga.miuix.kmp.basic.TopAppBar
import top.yukonga.miuix.kmp.icon.MiuixIcons
import top.yukonga.miuix.kmp.icon.extended.Back
import top.yukonga.miuix.kmp.theme.MiuixTheme
import keepass2android.miuix.data.DatabaseState
import keepass2android.miuix.data.KpEntry

@Composable
fun FavoritesScreen(
    extraEntries: List<KpEntry>,
    onBack: () -> Unit,
    onOpenEntry: (Int) -> Unit,
) {
    val colors = MiuixTheme.colorScheme
    val scrollBehavior = MiuixScrollBehavior()
    val favorites = remember(extraEntries) {
        (DatabaseState.entries + extraEntries).filter { it.isFavorite }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = "收藏",
                scrollBehavior = scrollBehavior,
                navigationIcon = {
                    IconButton(onClick = onBack, modifier = Modifier.padding(start = 12.dp)) {
                        Icon(MiuixIcons.Back, "返回")
                    }
                },
            )
        },
    ) { padding ->
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(
                start = 16.dp,
                end = 16.dp,
                top = padding.calculateTopPadding() + 12.dp,
                bottom = padding.calculateBottomPadding() + 32.dp,
            ),
        ) {
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    if (favorites.isEmpty()) {
                        Column(modifier = Modifier.padding(vertical = 40.dp).fillMaxWidth()) {
                            Text("还没有收藏的条目", color = colors.onSurfaceVariantSummary)
                        }
                    } else {
                        favorites.forEachIndexed { index, entry ->
                            EntryRow(entry) { onOpenEntry(entry.id) }
                            if (index != favorites.lastIndex) HorizontalDivider()
                        }
                    }
                }
            }
        }
    }
}
