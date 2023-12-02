using Ivi.Visa;
using Keysight.Visa;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal class Program
{
    static void log(object? msg, [CallerMemberName] string? caller = "") 
        => Console.WriteLine($"{caller}(): {msg}");

    static (AssemblyName gmAssembly, string? implVersion) CheckVisaImplementationInstalled()
    {
        return (getIviVisaAssemblyName(), getVisaImplVersion());

        AssemblyName getIviVisaAssemblyName()
        {
            try
            {
                // Get a VISA.NET library version.
                return typeof(GlobalResourceManager).Assembly.GetName();
                //log($"{name.Name} : version={name.Version}.");
            }
            catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException)
            {
                throw new VisaImplNotFoundException();
            }
        }

        string getVisaImplVersion()
        {
            // Check whether VISA Shared Components is installed before using VISA.NET.
            // If access VISA.NET without the visaConfMgr.dll library, an unhandled exception will
            // be thrown during termination process due to a bug in the implementation of the
            // VISA.NET Shared Components, and the application will crash.
            const string VISA_CONFMGR_DLL = "visaConfMgr.dll";
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory, VISA_CONFMGR_DLL));
                return vi.ProductVersion ?? $"{vi.ProductMajorPart}.{vi.ProductMinorPart}";
                //log($"VISA Shared Components version {vi.ProductVersion} detected.");
            }
            catch (FileNotFoundException) { throw new VisaImplNotFoundException(); }
            catch (VisaException ex) when (ex.Message == "No vendor-specific VISA .NET implementation is installed.")
            { throw new VisaImplNotFoundException(); }
        }
    }

    static void Main(string[] args)
    {
        (AssemblyName gmAssembly, string? implVersion)? _visaInfo = null;

        try
        {
            _visaInfo = CheckVisaImplementationInstalled();
            log($"_visaInfo={_visaInfo}");

            //var names = GlobalResourceManager.Find();
            using var rm = new ResourceManager();
            var rns = rm.Find("?*");

            var rn = "TCPIP0::127.0.0.1::29979::SOCKET";
            using var device = rm.Open(rn, AccessModes.ExclusiveLock, 100);
            log($"rn={rn}");
            log($"device={device.ResourceName}");
            //using var device = GlobalResourceManager.Open(rn, AccessModes.ExclusiveLock, 2000);
            if (device is IMessageBasedSession session)
            {
                // Ensure termination character is enabled as here in example we use a SOCKET connection.
                session.TerminationCharacterEnabled = true;
                session.TimeoutMilliseconds = 10000;

                // Request information about an instrument.
                session.FormattedIO.WriteLine("*IDN?");
                string idn = session.FormattedIO.ReadLine();
                log($"Instrument information: {idn}");
            }
            else
            {
                log("Not a message-based session.");
            }
        }
        catch (VisaImplNotFoundException)
        {
            // VISA Shared Components is not installed.
            log($"VISA implementation compatible with VISA.NET Shared Components not found."
                + " Please install corresponding vendor-specific VISA implementation first.");
        }
        catch (EntryPointNotFoundException)
        {
            // Installed VISA Shared Components is not compatible with VISA.NET Shared Components
            log($"Installed VISA Shared Components version {_visaInfo?.implVersion} does not support"
                + $" VISA.NET {_visaInfo?.gmAssembly.Version}. Please upgrade VISA implementation.");
        }
        catch (Exception ex)
        {
            log($"Exception: {ex.Message}");
        }
    }
}