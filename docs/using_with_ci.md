# Configuring CI to use RTF

This doc describes how to configure a CI system and use RevitTestFramework to run Revit tests on the CI machine.  
This document will describe setup for Microsoft Visual Studio and AWS EC2, however, the general instructions will work on other systems, e.g. Jenkins, TeamCity, etc.

1. **Setup Visual Studio Team Services**  
Visual Studio Team Services serves as a master in our CI. You can also use Jenkins for this purpose as well.  
You will need to host your own machine (steps below) for actually executing the build/test steps as that machine will have to have Revit installed (default VSTS machines, naturally, do not have it installed)
    1. Create an account if you don't have one yet
    1. Set up an agent pool (we have 2 pools one for general build/test and one for UI tests)
    1. Setup a new build pipeline:
        1. As an example, you can use one pipeline for build and one pipeline for UI tests
        1. Connect to your code repository (git/bitbucket/etc), this will popup oauth
        1. Specify an agent pool to run on 
           An agent pool is a pool of machines that can service a paricular pipeline. Since, in this example, we have 2 pipelines, we will use the `Default` pool for one of them and create a new one, `UI Tests` for the Revit UI tests pipeline
        1. Define the pipeline  
           These are steps that the build agent will perform to execute the tests, as an example, here are the steps for the pipeline we use:
           1. Get sources
           2. VsTest Platform Installer
           3. Build binaries (we use a PowerShell script to perform the build, but you can use the default MSBuild step)
           4. Run Revit UI Tests  
              We use a PowerShell script here as well, which essentially executes our RTF tests, e.g.:  
              ```
              RevitTestFrameworkConsole.exe --dir .\bin\Release -a MyTest.dll -r .\MyTestResults.xml -revit:"C:\Program Files\Autodesk\Revit 2019\Revit.exe" --continuous
              ```
           5. Publish Test Results  
              This steps takes the XML file produced by RTF and publishes it so that VSTS understands which tests pass and which fail.  
              Select *NUnit* for Test result format and your test result file, e.g. `.\MyTestResults.xml`
        1. At this point you can also configure triggers for each build pipeline (e.g. a pull request, or a merge into a specific branch, etc)

1. **Setup the build server**  
Now we setup a build machine that will connect to VSTS above and execute the build pipeline  
    1. Decide where to host the build machine (AWS/Azure/local/etc) and what Windows variant you want to use. We use AWS and run our CI on Windows Server 2016.
    1. Spin up a new instance, I recommend at least 16GB of RAM and 4 processors with ~150gb of storage (`m5.xlarge` on AWS)  
    *Security note:* Since you will need to remote into this machine, it needs to be open to the world. To mitigate security risks:
        * make sure you have a strong password (or use automatically generated AWS password) 
        * limit the IPs that are allowed to access this machine to the IP of your office (you can use whatsmyip.org)
    1. Remote into your new instance (as `localhost\Administrator`)
    1. Create a new **non-admin** account (e.g. `vsagent`) on the machine (make sure it's a strong password and you have it saved somewhere safe!)
    1. Setup some convenience stuff
        * Create a PowerShell shortcut on the desktop
        * Create a shortcut on the desktop to launch CMD as `vsagent` user, e.g. a shortcut to:
        `C:\tools\PsExec.exe -accepteula -w c:\agent -i -u vsagent cmd.exe`  
        This will allow you to run programs as `vsagent` user while being logged in as `Administrator`
    1. Download and install necessary software on the machine:  
       *Note:* you will not be able to use IE on the remote machine (if it's Windows Server) because it is locked down. I don't recommend removing the lockdown or installing some other browsers - this machine should be secure!  
       I recommend getting the link to each download on your personal computer, then pasting that link into a PowerShell on the remote, e.g.:
       ```PowerShell
       # Disable progress bar: makes downloads ~50x faster
       $ProgressPreference = "SilentlyContinue"
       
       # Download the file
       Invoke-WebRequest <PASTE URL> -OutFile <FileName>
       ```
        1. [Git](https://git-scm.com/download/win) (or other source control software needed to connect to your source code repository, use defaults)
        1. [Visual Studio](https://www.visualstudio.com/downloads/), scroll down to [VS Build tools](https://www.visualstudio.com/thank-you-downloading-visual-studio/?sku=BuildTools&rel=15)  
        Make sure to select the following components to be installed (in addition to defaults): 
            * .NET for desktop
            * Windows SDK (latest version)
            * Click-once tools
        1. [Wix](https://github.com/wixtoolset/wix3/releases/download/wix3111rtm/wix311.exe) or whatever other tool you need to build your installer/setup package.
            * If you use Wix, you will need to enable .NET 3.5 on the server, you can do this in the *Turn on Optional Features* control panel.  
            You will probably need to reboot after this.
            * Install Wix
            * You will need to add the following system-wide environment variables:
            ```
            WixCATargetsPath = "C:\Program Files (x86)\WiX Toolset v3.11\SDK\wix.ca.targets"
            WixTargetsPath = "C:\Program Files (x86)\MSBuild\Microsoft\WiX\v3.x\wix.targets"
            ```
        1. [PsTools](https://docs.microsoft.com/en-us/sysinternals/downloads/pstools), simply extract the .zip into a folder, e.g. `c:\tools`
        1. VsAgent click the *Download Agent* button on your VSTS account webpage. To install it, just unzip it to a new folder: `c:\agent`
        1. Revit (we use 2019.1 for our CI)
            * Once installed, you will need to run the Revit once to register it and dismiss any first run prompts (e.g. EULA, etc). This will require allowing Autodesk registration site to bypass IE security settings:
            * Open an interactive session to the `vsagent` user with `psexec` (or the shortcut we created above):  
            `C:\tools\PsExec.exe -i -u vsagent cmd.exe`
            * In the CMD for `vsagent`, open Internet Explorer:  
            `c:\Program Files\Internet Explorer\iexplore.exe`
            * In IE, click on the gear/*Tools* icon, *Internet Options*, *Security*, *Trusted Sites*, *Sites*, and add `https://registeronce.autodesk.com` to the list.
            * Close IE
            * Now, from the same command window, run Revit, e.g.  
            `C:\Program Files\Autodesk\Revit 2019\Revit.exe`
            * Accept all prompts, login, whatever you need to do, then close Revit.
            * For good measure, run Revit again and make sure no prompt show up - as they will cause issues with RTF
    1. At this point we have a machine that can run our CI/Revit UI tests, let's make a snapshot (AMI) of it so we can easily spin up as many of these as we want. For AWS/AMI:
        * (on the remote machine) Open `Ec2LaunchSettings`
        * Keep all settings, and click *Shutdown with Sysprep*
        * In the EC2 console, select the instance and chose *Image* -> *Create Image* from the *Actions* menu (give the image a descriptive name, e.g. `Revit Addin CI`)
        * This will take a few minutes. Once done you can use this AMI to spin up as many worker agents as you want.

1. Spin up a build server and connect it to Visual Studio Team Services
    1. If using an AMI, use that AMI. Optionally, you can skip the AMI step and use the machine instance from above directly (though it's not recommended)  
       Settings to consider:
        * You can assign an AWS role to the machine such that it has credentials to access any private resources you may need (e.g. if you want to deploy data to an S3 bucket)
        * You can create a security group which limits remote connection to your IP addresses and assign that security group to the machine
    1. Once the machine is booted up, remote into it.
       For credentials, use `localhost\Administrator` and the password you decrypt on AWS console (with the PEM file)
    1. You may want to consider installing code signing certificate at this point if you are signing your Revit Addin (highly recommended)
        1. Open CMD to `vsagent` (see steps above)
        1. Open `certmgr` from CMD
        1. Install the certificate into personal store
    1. Finally, configure the Visual Studio agent, open a PowerShell window and:
        ```PowerShell
        cd c:\agent
        .\config.cmd

        # Follow the prompts
        # Server url:          your VSTS url, e.g. https://[mybuild].visualstudio.com
        # Auth type:           PAT
        # PAT:                 (get it from https://[mybuild].visualstudio.com/_usersSettings/tokens)
        # Pool:                use default for CI and UI Tests for Revit (or whatever pools you created above)
        # Agent name:          e.g. REVIT-CI-1
        # Work folder:         [default value]
        # Run as service:      NO
        # Configure autologon: YES
        # Account to use:      vsagent [+ your password when prompted]
        ```
        Reboot.
    1. Once rebooted you should see your new agent on Visual Studio Team Services webpage    
    1. Queue up a new build and see if it works!

1. Debugging when things go wrong...
   In some cases, you just need to remote into the build machine to debug a broken build.  
   Since the build runs as the `vsagent` account but `vsagent` account can remote into the machine, you will have to follow these steps:
    1. Open AWS console, EC2 instances
    2. Select the instance and click *Connect*
    3. Use the PEM file to decrypt the password
    4. Once connected (you will always connect with `localhost\Administrator` account), open *Task Manager* and click on *Users* then right click on `vsagent` and click connect.
    5. Now you can run and debug on the live machine