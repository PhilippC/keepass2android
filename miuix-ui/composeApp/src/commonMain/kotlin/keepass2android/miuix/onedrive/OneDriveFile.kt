package keepass2android.miuix.onedrive

expect class OneDriveFile(name: String, path: String, sizeBytes: Long) {
    val name: String
    val path: String
    val sizeBytes: Long
}
