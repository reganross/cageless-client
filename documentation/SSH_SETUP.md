# User Manual: Universal SSH Key Setup & Management

## 1. Overview
Secure Shell (SSH) keys provide a secure, password-less way to authenticate with remote services. This guide covers the generation, registration, and usage of keys across **Windows 11**, **macOS**, and **Linux**.

---

## 2. Generating Your Keys
To create a new key pair, use the `ssh-keygen` command. This creates a "Private Key" (your secret identity) and a "Public Key" (the lock you give to others).

### The Command Breakdown:
Run the following in your terminal:
```bash
ssh-keygen -t ed25519 -C "{comment}" -f ~/.ssh/{key-name}
```

**What each part does:**
* **`ssh-keygen`**: The tool that generates the keys.
* **`-t ed25519`**: Specifies the algorithm. **Ed25519** is the modern standard—it is faster and more secure than older types like RSA.
* **`-C "{comment}"`**: Adds a label to the end of your public key file. 
    * **Note:** Most developers use their email address here (e.g., `"your-email@example.com"`), but you can use something descriptive like `"Home-PC"` or `"Galaxy-S23"`.
* **`-f ~/.ssh/{key-name}`**: Specifies the **filename**. Replace `{key-name}` with a descriptive name (e.g., `github`, `gitlab`, `prod_server`).

> **Important:** When prompted for a passphrase, you can enter one for extra security. **Note that you will not see any characters appear on the screen as you type**; this is a standard security feature. Just type your phrase and hit `Enter`.

---

## 3. Configuring the SSH Agent
The "Agent" is a background service that holds your keys in memory so you don't have to type your passphrase every time you connect.

### **Windows 11**
Run **PowerShell as Administrator**:
```powershell
# Start the service and set it to run automatically
Start-Service ssh-agent
Set-Service -Name ssh-agent -StartupType Automatic

# Register your key with the agent
ssh-add ~/.ssh/{key-name}
```

### **macOS**
```bash
# Add the key to the agent and save the passphrase to your Apple Keychain
ssh-add --apple-use-keychain ~/.ssh/{key-name}
```

### **Linux**
```bash
# Start the agent for the current session
eval "$(ssh-agent -s)"

# Add your key
ssh-add ~/.ssh/{key-name}
```

---

## 4. Automating with a Config File (Recommended)
To avoid running `ssh-add` every time you reboot, you can create an SSH configuration file. This tells your computer exactly which key to use for which site.

1. **Create the file:** In your `~/.ssh/` folder, create a file named exactly `config` (with no file extension).
2. **Add your settings:** Open it in a text editor and add a block for each service:
   ```text
   # GitHub settings
   Host github.com
     HostName github.com
     User git
     IdentityFile ~/.ssh/{key-name}

   # Example: Private Server settings
   Host myserver
     HostName 192.168.1.50
     User username
     IdentityFile ~/.ssh/{other-key-name}
   ```

---

## 5. Connecting to a Service
To use your key, you must provide the **Public** version to the service provider.

1.  **Locate the Public Key:** This is the file ending in `.pub` (e.g., `~/.ssh/{key-name}.pub`).
2.  **Copy the Content:**
    * **Windows:** `Get-Content ~/.ssh/{key-name}.pub | Set-Clipboard`
    * **macOS:** `pbcopy < ~/.ssh/{key-name}.pub`
    * **Linux:** `cat ~/.ssh/{key-name}.pub`
3.  **Paste:** Log into your service (GitHub, GitLab, etc.) or provide the text to your Server Administrator.

---

## 6. Summary Table

| Component | Purpose | Security |
| :--- | :--- | :--- |
| **Private Key** | Your secret identity for signing in. | **Keep Secret!** |
| **Public Key** | The "lock" you upload to the service. | Shared freely. |
| **Config File** | Maps keys to specific websites/servers. | System-level. |
| **ssh-agent** | Background service that manages keys. | System-level. |

---

## 7. Verification
Test your connection with the following command:

`ssh -T {user}@{host}`

**Example for GitHub:** `ssh -T git@github.com`  
*If successful, the terminal will return a greeting confirming your authentication.*

### Troubleshooting Tips
* **Permissions (Mac/Linux):** Your private key must be protected. Run `chmod 600 ~/.ssh/{key-name}` if you get a "permissions are too open" error.
* **Permissions (Windows):** If you get a permission error, right-click your `.ssh` folder > Properties > Security, and ensure only your User account has access.
* **Filename Consistency:** Ensure the name used in **Step 2** (`-f`) is exactly the same as the one used in **Step 3** and **Step 4**.