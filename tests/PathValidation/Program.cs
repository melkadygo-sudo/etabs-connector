using EtabsConnector;
Environment.SetEnvironmentVariable("ETABS_CONNECTOR_ALLOWED_ROOTS", @"C:\LegacyRestrictedFolder");
Environment.SetEnvironmentVariable("ETABS_CONNECTOR_ALLOW_WRITES", null);
Environment.SetEnvironmentVariable("ETABS_CONNECTOR_ALLOW_ANALYSIS", null);
var settings = ConnectorSettings.FromEnvironment();
foreach (var path in new[] { @"C:\Other Project\model.edb", @"D:\Projects\model.edb", @"\\server\share\model.edb" })
    if (settings.ValidateModelPath(path, false) != Path.GetFullPath(path)) throw new Exception("Model path rejected");
settings.ValidateFileSystemPath(@"D:\Reports\output.csv", false);
foreach (var invalid in new[] { "", @"C:\Other Project\model.txt" })
{
    try { settings.ValidateModelPath(invalid, false); throw new Exception("Invalid path accepted"); }
    catch (ConnectorException) { }
}
if (settings.AllowWrites || settings.AllowAnalysis) throw new Exception("Permission defaults changed");
Console.WriteLine("PASS: arbitrary drive and UNC paths, legacy roots ignored, invalid model paths rejected, read-only defaults preserved.");
