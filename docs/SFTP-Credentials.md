# SFTP Open/Create Database Credentials Documentation

## Basic Settings
* **Host** -- the hostname or IP address of the SFTP server to connect to
* **Port** -- the listening TCP port of the SFTP server to connect to (default: 22)
* **Username** -- the user/account name on the SFTP server that has access to the database
* **Initial directory** -- The path on the SFTP server that will be used as a starting point when choosing the remote database file

### Authentication Modes

#### Password
Authenticate using a password

* **Password** -- the password associated with **username** used to log into the SFTP server

#### K2A Private/Public Key
Authenticate using a private/public key pair that is generated internally by KP2A

* **SEND PUBLIC KEY...** -- Opens a standard Android "Share" screen containing the KP2A public key content. This allows for the public key to be sent via email, SMS, etc. This public key will need to be added to the SFTP server's user's "authorized keys" to allow private/public key authentication.

#### Custom Private Key
Authenticate using an existing private/public key pair. Use this option instead of *K2A Private/Public Key* if you wish to use a key pair that is already set up for this **username** on the SFTP server.

* **Selected private key** -- a combo-box containing a list of custom private keys that KP2A knows about, and a special `[Add new...]` option.
##### Add A New Private Key
* Select `[Add new...]`
* Enter a name for the new key in **New key name**
* Enter the private key contents (text) into **New key content**. **TIP:** The easiest way to accomplish this is to open the private key file in a text editor on the device, **Select All**, **Copy** to the clipboard, and paste it into **New key content**.
* Tap **SAVE PRIVATE KEY** to add the new key to the known list.

##### Use An Existing Private Key
* To use a private key that has already been imported into KP2A, simply select it from the list of keys.

##### Remove An Existing Key
* To remove a private that has been imported into KP2A, select it from the list and tap **DELETE PRIVATE KEY**.

A **key passphrase** can be supplied (if the key pair requires it)

## Advanced Settings
* **Connection timeout seconds** -- the number of seconds to wait for a connection to the server before giving up and considering the server as unavailable/unreachable

### Key Algorithm Manipulation
**NOTE: It is very rare that these fields need to be (or should be) specified. Use at your own risk!**

* **Key Exchange (KEX) Algorithm(s)** -- Explicitly set or modify the ordered list of Key Exchange algorithms that the SSH/SFTP client library will try to use
* **Server Host Key Algorithm(s)** -- Explicitly set or modify the ordered list of Server Host Key algorithms that the SSH/SFTP client library will try to use

#### How It Works
The SSH/SFTP client has a pre-defined ordered list of algorithm names that it will use to negotiate with the server to handle key exchange. In rare cases there are compatibility issues where Android OS has not properly implemented full support for algorithms listed. This can result in a connection failure, even if there is a suitable algorithm available (of lesser priority in the list).

The fields listed above allow these lists to be manipulated in the following ways to overcome/workaround such problems. The value is a comma-separated list of "algorithm spec" entries. Specs can be one of:

* Direct replacement of values -- Ex: `primary_alg,secondary_alg`
* Prepend to values -- Ex: `+try_first_alg`
* Append to values -- Ex: `try_last_alg+`
* Remove a specific value -- Ex: `-bad_alg`
* Remove values matching prefix -- Ex: `-bad_starting_with*`
* Remove values matching suffix -- Ex: `-*bad_ending_with`
* Remove values matching substring -- Ex: `-*bad_middle*`
* Remove values matching prefix and suffix -- Ex: `-alg_begin*end`

For example, assume the system's KEX algorithm list is:
`ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256`

These are various outcomes (user KEX field -> result):

* Prefix removal: `-ec*` --> `diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256`
* Suffix removal, appending: `-*256,+first_alg,almost_last_alg+,last_alg+` --> `first_alg,ecdh-sha2-nistp384,ecdh-sha2-nistp521,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,almost_last_alg,last_alg`
* Direct replacement: `first_alg,middle_alg,last_alg` --> `first_alg,middle_alg,last_alg`

## Selecting A Database
Once all applicable fields have been entered and/or options selected, tapping **OK** will attempt to connect to the SFTP server. First time connections may pop up a dialog window asking to accept the host's authenticity (tap **yes** if the host is trusted), as well as potentially creating a new `known_hosts` file (tap **yes** to do so). If the connection is successful, a remote file browser screen will open. Navigate and select the Keepass database to open.