TFSBlaster makes it easy to create Azure DevOps work items en masse from simple-yet-powerful JSON definitions.

### Table of Contents
- [Warnings](#warnings)
- [How to blast](#howto)
- [Tips](#tips)
- [Troubleshooting](#troubleshoot)

<a name="warnings" />

# Warnings

It's **really easy to make a mess** with this tool if you're not careful, as it does not do much to prevent you from accidentally running the same actions multiple times (unless those actions happen to create an illegal conflict in the work items). And while I included a fair amount of validation to prevent mistakes, there's also no rollback, so if the first few actions make some changes and a later action fails then you'll need to do some manual cleanup in the affected work items and/or the action script to avoid repeating the successful actions unnecessarily.

I originally made this tool for my personal use at a particular organization. To use it at other organizations, you may need to modify some of the code (e.g. some work item fields have organization-specific names that are currently just hard-coded). **You should really read and understand the code**, as I leaned on my own familiarity with it a lot throughout the time I used it.

The command line output isn't super helpful when things go wrong, you'll need to inspect the log.txt file that gets generated in the runtime workspace. The log itself also does not hold your hand, most problems require digging into HTTP response or JSON exception dumps and understanding what they mean in the context of ADO or JSON.

I might check in on this repo occasionally and try to address any issues or requests that come up, but in general it's safer to assume **this project is no longer actively supported**. For significant improvements I highly recommend forking to make them yourself.

<a name="howto"/>

# How to blast

1. You need an account that's authorized to create and modify work items in your organization's ADO system.
2. Create a personal access token for your account: [https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate)
    - The only access scope you need to enable for this token is read/write/manage for Work Items
    - When you generate the token, be careful not to close the browser tab or navigate it to another page until you've finished this process - you'll never be able to see this token again!
3. Copy the access token into a file named credentials.json with this format:
```
{
  "UserName": "", //leave this as an empty string
  "Password": "your account's access token"
}
```
4. Open the solution. I wrote it using VS 2019, so I recommend using that IDE.
5. Create a .json file and define actions for your blast.
    - See the inline comments in [TFSBlaster/Data/example.json](TFSBlaster/Data/example.json) for more detail on how to do this.
    - You can create this file anywhere in your file system and it doesn't have to be added to the project. This may be necessary if you're going to develop this tool further and contribute changes publicly but don't want to expose your organization's sensitive information in your JSON definitions. However I've found it's a lot easier to do everything in the project and copy .json files to build output so relative paths are simpler and machine-independent.
6. Build and run with two command line arguments: the path to your blast .json file, and the path to your credentials.json file.
7. Command line output should clearly indicate whether the blast was successful or not. If it wasn't, you can find log.txt in the build output directory and investigate that to determine what happened.

<a name="tips" />

# Tips

You can work with multiple .json files, as I did initially, but that tends to become unmanageable as projects change and grow and multiply. I ended up using a single big .json file most of the time, and commented/uncommented things using the VS commands Ctrl+K+C and Ctrl+K+U as needed. You just need to be careful not to create JSON syntax errors if you do this (it's especially easy to miss a comma, for example). Fortunately, JSON syntax errors are caught quite early and will prevent the entire blast from running.

WorkItemDefaults are probably the most powerful part of this tool, if you use them well you can save yourself a lot of trouble. You can define them for every work item profile in the blast, or for every profile in a particular action.

I expected the TextReplacements feature to be more useful than it actually was. It doesn't end up saving you much trouble because you have to double-check everything that uses them to make sure you still want to use them in the way you wrote them 10 blasts ago. It does help ensure consistency when you have a lot of repetition across work items though.

Bigger text fields like task descriptions let you use HTML to format the text you inject into them. Use it in your blast! At the very least for hyperlinks and line breaks.

<a name="troubleshoot" />

# Troubleshooting

### TFSBlaster didn't make the changes in my template, but the console output doesn't show any errors
Look for a log.txt file next to the TFSBlaster executable, it should contain high-detail error (and non-error) messages.

### Error logged: HTTP client failed to send request... HttpRequestException... The handshake failed...
Check your TfsUri in your JSON file. If the protocol is https://, try changing it to http://. It may be caused by the use of a security protocol that isn't explicitly enabled in this application yet, in that case if you must use https consider modifying the code to ignore invalid certs and/or enable the right protocol version (e.g. TLS 1.2): [https://stackoverflow.com/a/52965270/2711241](https://stackoverflow.com/a/52965270/2711241)
