package keepass2android.miuix.data

/**
 * 当前会话的数据库内容。
 * 未解锁真实数据库时回退到演示数据；解锁成功后切换为真实数据。
 */
object DatabaseState {
    private var realGroups: List<KpGroup>? = null
    private var realEntries: List<KpEntry>? = null

    val isReal: Boolean get() = realGroups != null

    val groups: List<KpGroup> get() = realGroups ?: DemoData.groups
    val entries: List<KpEntry> get() = realEntries ?: DemoData.entries

    fun loadReal(groups: List<KpGroup>, entries: List<KpEntry>) {
        realGroups = groups
        realEntries = entries
    }

    fun reset() {
        realGroups = null
        realEntries = null
    }

    fun subgroupsOf(groupId: Int): List<KpGroup> = groups.filter { it.parentId == groupId }

    fun entriesOf(groupId: Int): List<KpEntry> = entries.filter { it.groupId == groupId }

    fun findEntry(id: Int): KpEntry? = entries.firstOrNull { it.id == id }

    fun findGroup(id: Int): KpGroup? = groups.firstOrNull { it.id == id }

    fun entryCountRecursive(groupId: Int): Int {
        val direct = entriesOf(groupId).size
        return direct + subgroupsOf(groupId).sumOf { entryCountRecursive(it.id) }
    }
}
