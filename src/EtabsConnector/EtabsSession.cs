using ETABSv1;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace EtabsConnector;

internal sealed class EtabsSession : IDisposable
{
    private const string HelperProgId = "ETABSv1.Helper";
    private const string EtabsProgId = "CSI.ETABS.API.ETABSObject";
    private const int ObjectElm = 0;

    private readonly ConnectorSettings _settings;
    private cHelper? _helper;
    private cOAPI? _application;
    private cSapModel? _sapModel;
    private bool _startedByConnector;
    private string? _workingCopyPath;

    public EtabsSession(ConnectorSettings settings)
    {
        _settings = settings;
    }

    public object GetCapabilities() => new
    {
        product = "ETABS",
        supported_major_version = 23,
        connector_runtime = ".NET 8, Windows x64",
        transport = "MCP stdio",
        writes_enabled = _settings.AllowWrites,
        analysis_enabled = _settings.AllowAnalysis,
        allowed_roots = _settings.AllowedRoots,
        maximum_result_rows = _settings.MaximumResultRows,
        working_copy_required_for_writes = false,
        model_copy_optional_for_writes = true,
        automatic_unlock_for_authorized_operations = true,
        full_etabs_api_gateway = true,
        installed_api_interface_count = 140,
        installed_api_method_count = 1453,
        tools = ToolCatalog.Names
    };

    public object Connect(bool startIfMissing)
    {
        if (_sapModel is not null)
            return GetModelInfo();

        try
        {
            var helperType = Type.GetTypeFromProgID(HelperProgId, throwOnError: true)!;
            _helper = Activator.CreateInstance(helperType) as cHelper
                ?? throw new ConnectorException("Could not create the typed ETABS API helper.");

            try
            {
                _application = _helper.GetObject(EtabsProgId);
            }
            catch when (startIfMissing)
            {
                _application = _helper.CreateObjectProgID(EtabsProgId);
                Check((int)_application.ApplicationStart(), "ApplicationStart");
                _startedByConnector = true;
            }

            _sapModel = _application.SapModel
                ?? throw new ConnectorException("ETABS did not return a SapModel object.");
            TryAdoptActiveWorkingCopy();
            return GetModelInfo();
        }
        catch (ConnectorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConnectorException(
                "Could not connect to ETABS 23. Confirm ETABS is installed, licensed, and running, or use start_if_missing=true.",
                ex);
        }
    }

    public object OpenModel(string path, bool confirmReplaceActive)
    {
        EnsureConnected();
        var fullPath = _settings.RequireAllowedPath(path, createParent: false);
        if (!File.Exists(fullPath))
            throw new ConnectorException($"Model file does not exist: {fullPath}");

        var activePath = (string)_sapModel!.GetModelFilename(true);
        if (!string.IsNullOrWhiteSpace(activePath) && !confirmReplaceActive)
            throw new ConnectorException(
                "ETABS already has a model open. Set confirm_replace_active=true only after confirming that unsaved changes are not required.");

        Check((int)_sapModel.File.OpenFile(fullPath), "File.OpenFile");
        _workingCopyPath = null;
        return GetModelInfo();
    }

    public object GetModelInfo()
    {
        EnsureConnected();
        var modelPath = (string)_sapModel!.GetModelFilename(true);
        var unitsValue = Convert.ToInt32(_sapModel.GetPresentUnits());
        var locked = (bool)_sapModel.GetModelIsLocked();
        var storeys = GetStoreyInfo();
        var analysisStatus = GetAnalysisStatus();
        return new
        {
            connected = true,
            product = "ETABS",
            // Attachment uses the ETABS 23 ProgID. Avoid SapModel.GetVersion:
            // late-bound ref/out COM marshalling can corrupt SAFEARRAY cleanup.
            version = "ETABS 23",
            version_number = 23d,
            model_path = modelPath,
            model_locked = locked,
            present_units = EtabsUnits.NameOf(unitsValue),
            point_count = SafeCount(() => (int)_sapModel.PointObj.Count()),
            frame_count = SafeCount(() => (int)_sapModel.FrameObj.Count()),
            area_count = SafeCount(() => (int)_sapModel.AreaObj.Count()),
            storeys,
            analysis_status = analysisStatus,
            application_started_by_connector = _startedByConnector,
            working_copy_active = IsWorkingCopyActive(modelPath),
            working_copy_path = _workingCopyPath
        };
    }

    public object ListLoadDefinitions()
    {
        EnsureConnected();
        return new
        {
            load_cases = GetNameList((object)_sapModel!.LoadCases, "LoadCases.GetNameList"),
            load_combinations = GetNameList((object)_sapModel.RespCombo, "RespCombo.GetNameList")
        };
    }

    public object ListObjects(string objectType, int maxResults)
    {
        EnsureConnected();
        var names = objectType.Trim().ToLowerInvariant() switch
        {
            "point" => GetNameList((object)_sapModel!.PointObj, "PointObj.GetNameList"),
            "frame" => GetNameList((object)_sapModel!.FrameObj, "FrameObj.GetNameList"),
            "area" => GetNameList((object)_sapModel!.AreaObj, "AreaObj.GetNameList"),
            _ => throw new ConnectorException("object_type must be point, frame, or area.")
        };
        var limit = ValidateLimit(maxResults);
        return new
        {
            object_type = objectType,
            total_count = names.Length,
            returned_count = Math.Min(names.Length, limit),
            truncated = names.Length > limit,
            names = names.Take(limit).ToArray()
        };
    }

    public object ListProperties(string propertyType, int maxResults)
    {
        EnsureConnected();
        var names = propertyType.Trim().ToLowerInvariant() switch
        {
            "frame" => GetNameList((object)_sapModel!.PropFrame, "PropFrame.GetNameList"),
            "area" => GetNameList((object)_sapModel!.PropArea, "PropArea.GetNameList"),
            _ => throw new ConnectorException("property_type must be frame or area.")
        };
        var limit = ValidateLimit(maxResults);
        return new
        {
            property_type = propertyType,
            total_count = names.Length,
            returned_count = Math.Min(names.Length, limit),
            truncated = names.Length > limit,
            names = names.Take(limit).ToArray()
        };
    }

    public object GetFrameSection(string frameName)
    {
        EnsureConnected();
        string section = string.Empty;
        string autoSelect = string.Empty;
        Check((int)_sapModel!.FrameObj.GetSection(frameName, ref section, ref autoSelect),
            $"FrameObj.GetSection({frameName})");
        return new { frame_name = frameName, section_name = section, auto_select_list = autoSelect };
    }

    public object GetAreaProperty(string areaName)
    {
        EnsureConnected();
        string property = string.Empty;
        Check((int)_sapModel!.AreaObj.GetProperty(areaName, ref property),
            $"AreaObj.GetProperty({areaName})");
        return new { area_name = areaName, property_name = property };
    }

    public object GetPointRestraint(string pointName)
    {
        EnsureConnected();
        var restraint = new bool[6];
        Check((int)_sapModel!.PointObj.GetRestraint(pointName, ref restraint),
            $"PointObj.GetRestraint({pointName})");
        return new { point_name = pointName, restraint, order = new[] { "U1", "U2", "U3", "R1", "R2", "R3" } };
    }

    public object SaveWorkingCopy(string path)
    {
        EnsureWriteEnabled();
        EnsureConnected();
        var fullPath = _settings.RequireAllowedPath(path, createParent: true);
        var sourcePath = (string)_sapModel!.GetModelFilename(true);
        if (!string.IsNullOrWhiteSpace(sourcePath)
            && Path.GetFullPath(sourcePath).Equals(fullPath, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorException("The working copy path must differ from the active source model.");
        if (File.Exists(fullPath))
            throw new ConnectorException(
                $"The working copy already exists. Choose a new path to prevent overwrite: {fullPath}");

        Check((int)_sapModel.File.Save(fullPath), "File.Save working copy");
        var readBackPath = (string)_sapModel.GetModelFilename(true);
        var readBackFull = Path.GetFullPath(readBackPath);
        var verifiedPath = readBackFull.Equals(fullPath, StringComparison.OrdinalIgnoreCase)
            || (Path.GetExtension(readBackFull).Equals(".$et", StringComparison.OrdinalIgnoreCase)
                && Path.ChangeExtension(readBackFull, ".edb").Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (!verifiedPath || !File.Exists(fullPath))
            throw new ConnectorException($"Working copy verification failed. ETABS reports: {readBackPath}");
        _workingCopyPath = fullPath;
        return new
        {
            source_path = sourcePath,
            working_copy_path = fullPath,
            verified = true
        };
    }

    public object SetPresentUnits(string units)
    {
        RequireWriteReady();
        var beforeValue = Convert.ToInt32(_sapModel!.GetPresentUnits());
        var requestedValue = EtabsUnits.Parse(units);
        Check((int)_sapModel.SetPresentUnits((eUnits)requestedValue), "SetPresentUnits");
        var afterValue = Convert.ToInt32(_sapModel.GetPresentUnits());
        if (afterValue != requestedValue)
            throw new ConnectorException("Present units read-back verification failed.");

        return new
        {
            before = EtabsUnits.NameOf(beforeValue),
            after = EtabsUnits.NameOf(afterValue),
            verified = true
        };
    }

    public object SetFrameSection(string frameName, string sectionName)
    {
        RequireWriteReady();
        string before = string.Empty;
        string beforeAuto = string.Empty;
        Check((int)_sapModel!.FrameObj.GetSection(frameName, ref before, ref beforeAuto),
            $"FrameObj.GetSection({frameName})");
        Check((int)_sapModel.FrameObj.SetSection(frameName, sectionName),
            $"FrameObj.SetSection({frameName})");

        string after = string.Empty;
        string afterAuto = string.Empty;
        Check((int)_sapModel.FrameObj.GetSection(frameName, ref after, ref afterAuto),
            $"FrameObj.GetSection verification({frameName})");
        if (!after.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorException("Frame section read-back verification failed.");

        return ChangeResult("frame_section", frameName, before, after);
    }

    public object CloneWallProperty(string sourceName, string targetName, double thickness)
    {
        RequireWriteReady();
        if (thickness <= 0 || double.IsNaN(thickness) || double.IsInfinity(thickness))
            throw new ConnectorException("Wall thickness must be a finite positive number in the present length units.");
        if (sourceName.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorException("Source and target wall property names must differ.");
        var existing = GetNameList((object)_sapModel!.PropArea, "PropArea.GetNameList");
        if (existing.Contains(targetName, StringComparer.OrdinalIgnoreCase))
            throw new ConnectorException($"Area property already exists: {targetName}");
        eWallPropType wallType = default;
        eShellType shellType = default;
        string material = string.Empty;
        double sourceThickness = 0;
        int color = -1;
        string notes = string.Empty;
        string sourceGuid = string.Empty;
        Check(_sapModel.PropArea.GetWall(sourceName, ref wallType, ref shellType, ref material,
            ref sourceThickness, ref color, ref notes, ref sourceGuid), $"PropArea.GetWall({sourceName})");
        double[] modifiers = Array.Empty<double>();
        Check(_sapModel.PropArea.GetModifiers(sourceName, ref modifiers), $"PropArea.GetModifiers({sourceName})");
        Check(_sapModel.PropArea.SetWall(targetName, wallType, shellType, material, thickness,
            color, notes, string.Empty), $"PropArea.SetWall({targetName})");
        Check(_sapModel.PropArea.SetModifiers(targetName, ref modifiers), $"PropArea.SetModifiers({targetName})");
        eWallPropType verifyWallType = default;
        eShellType verifyShellType = default;
        string verifyMaterial = string.Empty;
        double verifyThickness = 0;
        int verifyColor = -1;
        string verifyNotes = string.Empty;
        string verifyGuid = string.Empty;
        Check(_sapModel.PropArea.GetWall(targetName, ref verifyWallType, ref verifyShellType,
            ref verifyMaterial, ref verifyThickness, ref verifyColor, ref verifyNotes, ref verifyGuid),
            $"PropArea.GetWall verify({targetName})");
        if (Math.Abs(verifyThickness - thickness) > Math.Max(1e-9, Math.Abs(thickness) * 1e-9))
            throw new ConnectorException($"Wall property verification failed for {targetName}: expected {thickness}, got {verifyThickness}.");
        return new
        {
            operation = "clone_wall_property",
            source_name = sourceName,
            target_name = targetName,
            source_thickness = sourceThickness,
            target_thickness = verifyThickness,
            present_units = EtabsUnits.NameOf(Convert.ToInt32(_sapModel.GetPresentUnits())),
            material = verifyMaterial,
            wall_type = verifyWallType.ToString(),
            shell_type = verifyShellType.ToString(),
            modifiers,
            verified = true,
            working_copy_path = _workingCopyPath
        };
    }
    public object SetGroupAreaProperty(string groupName, string propertyName)
    {
        RequireWriteReady();
        var properties = GetNameList((object)_sapModel!.PropArea, "PropArea.GetNameList");
        if (!properties.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            throw new ConnectorException($"Area property does not exist: {propertyName}");
        int groupCount = 0;
        string[] groupNames = Array.Empty<string>();
        Check(_sapModel.GroupDef.GetNameList(ref groupCount, ref groupNames), "GroupDef.GetNameList");
        if (!groupNames.Take(groupCount).Contains(groupName, StringComparer.OrdinalIgnoreCase))
            throw new ConnectorException($"Group does not exist: {groupName}");
        int assignmentCount = 0;
        int[] objectTypes = Array.Empty<int>();
        string[] objectNames = Array.Empty<string>();
        Check(_sapModel.GroupDef.GetAssignments(groupName, ref assignmentCount, ref objectTypes, ref objectNames),
            $"GroupDef.GetAssignments({groupName})");
        var usable = new[] { assignmentCount, objectTypes.Length, objectNames.Length }.Min();
        var areaNames = Enumerable.Range(0, usable)
            .Where(i => objectTypes[i] == 5)
            .Select(i => objectNames[i])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (areaNames.Length == 0)
            throw new ConnectorException($"Group contains no area objects: {groupName}");
        var before = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var areaName in areaNames)
        {
            string assigned = string.Empty;
            Check(_sapModel.AreaObj.GetProperty(areaName, ref assigned), $"AreaObj.GetProperty({areaName})");
            before[areaName] = assigned;
        }
        Check(_sapModel.AreaObj.SetProperty(groupName, propertyName, eItemType.Group),
            $"AreaObj.SetProperty group({groupName})");
        foreach (var areaName in areaNames)
        {
            string assigned = string.Empty;
            Check(_sapModel.AreaObj.GetProperty(areaName, ref assigned), $"AreaObj.GetProperty verify({areaName})");
            if (!assigned.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                throw new ConnectorException($"Group property verification failed for area {areaName}: expected {propertyName}, got {assigned}.");
        }
        return new
        {
            operation = "group_area_property",
            group_name = groupName,
            property_name = propertyName,
            verified_area_count = areaNames.Length,
            previous_properties = before.Values.Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            verified = true,
            working_copy_path = _workingCopyPath
        };
    }
    public object SetAreaProperty(string areaName, string propertyName)
    {
        RequireWriteReady();
        string before = string.Empty;
        Check((int)_sapModel!.AreaObj.GetProperty(areaName, ref before),
            $"AreaObj.GetProperty({areaName})");
        Check((int)_sapModel.AreaObj.SetProperty(areaName, propertyName),
            $"AreaObj.SetProperty({areaName})");

        string after = string.Empty;
        Check((int)_sapModel.AreaObj.GetProperty(areaName, ref after),
            $"AreaObj.GetProperty verification({areaName})");
        if (!after.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorException("Area property read-back verification failed.");

        return ChangeResult("area_property", areaName, before, after);
    }

    public object SetPointRestraint(string pointName, bool[] restraint)
    {
        RequireWriteReady();
        if (restraint.Length != 6)
            throw new ConnectorException("restraint must contain exactly six booleans: U1, U2, U3, R1, R2, R3.");

        var before = new bool[6];
        Check((int)_sapModel!.PointObj.GetRestraint(pointName, ref before),
            $"PointObj.GetRestraint({pointName})");
        Check((int)_sapModel.PointObj.SetRestraint(pointName, ref restraint),
            $"PointObj.SetRestraint({pointName})");

        var after = new bool[6];
        Check((int)_sapModel.PointObj.GetRestraint(pointName, ref after),
            $"PointObj.GetRestraint verification({pointName})");
        if (!after.SequenceEqual(restraint))
            throw new ConnectorException("Point restraint read-back verification failed.");

        return new
        {
            operation = "point_restraint",
            object_name = pointName,
            before,
            after,
            verified = true,
            working_copy_path = _workingCopyPath
        };
    }

    public object RunAnalysis()
    {
        RequireWriteReady();
        if (!_settings.AllowAnalysis)
            throw new ConnectorException("Analysis is disabled. Set ETABS_CONNECTOR_ALLOW_ANALYSIS=true.");

        var started = DateTimeOffset.UtcNow;
        Check((int)_sapModel!.Analyze.RunAnalysis(), "Analyze.RunAnalysis");
        var locked = (bool)_sapModel.GetModelIsLocked();
        return new
        {
            completed = true,
            model_locked = locked,
            started_at_utc = started,
            completed_at_utc = DateTimeOffset.UtcNow,
            working_copy_path = _workingCopyPath,
            warning = "Solver completion is not engineering validation. Review the ETABS analysis log and model behaviour."
        };
    }

    public object GetJointGlobalYDisplacement(string pointName, string caseName)
    {
        EnsureConnected();
        var loadCases = GetNameList((object)_sapModel!.LoadCases, "LoadCases.GetNameList");
        if (!loadCases.Contains(caseName, StringComparer.OrdinalIgnoreCase))
            throw new ConnectorException($"Load case does not exist: {caseName}");
        PrepareOutputSelection([caseName], []);
        int count = 0;
        string[] obj = Array.Empty<string>();
        string[] elm = Array.Empty<string>();
        string[] loadCase = Array.Empty<string>();
        string[] stepType = Array.Empty<string>();
        double[] stepNum = Array.Empty<double>();
        double[] u1 = Array.Empty<double>();
        double[] u2 = Array.Empty<double>();
        double[] u3 = Array.Empty<double>();
        double[] r1 = Array.Empty<double>();
        double[] r2 = Array.Empty<double>();
        double[] r3 = Array.Empty<double>();
        Check(_sapModel.Results.JointDispl(pointName, (eItemTypeElm)0, ref count, ref obj, ref elm,
            ref loadCase, ref stepType, ref stepNum, ref u1, ref u2, ref u3, ref r1, ref r2, ref r3),
            $"Results.JointDispl({pointName})");
        var rowCount = new[] { count, obj.Length, elm.Length, loadCase.Length, u2.Length }.Min();
        if (rowCount == 0)
            throw new ConnectorException($"No joint displacement results were returned for point {pointName}, case {caseName}.");
        var rows = Enumerable.Range(0, rowCount).Select(i => new
        {
            point_object = obj[i],
            point_element = elm[i],
            load_case = loadCase[i],
            step_type = i < stepType.Length ? stepType[i] : string.Empty,
            step_number = i < stepNum.Length ? stepNum[i] : 0,
            global_y_displacement = u2[i]
        }).ToArray();
        return new
        {
            point_name = pointName,
            case_name = caseName,
            direction = "Global Y (U2)",
            displacement_units = "mm",
            result_count = rowCount,
            rows,
            maximum_absolute = rows.OrderByDescending(row => Math.Abs(row.global_y_displacement)).First(),
            model_path = (string)_sapModel.GetModelFilename(true)
        };
    }
    public object GetGlobalYDisplacementEnvelope(string caseName)
    {
        EnsureConnected();
        var loadCases = GetNameList((object)_sapModel!.LoadCases, "LoadCases.GetNameList");
        if (!loadCases.Contains(caseName, StringComparer.OrdinalIgnoreCase))
            throw new ConnectorException($"Load case does not exist: {caseName}");
        PrepareOutputSelection([caseName], []);
        int count = 0;
        string[] obj = Array.Empty<string>();
        string[] elm = Array.Empty<string>();
        string[] loadCase = Array.Empty<string>();
        string[] stepType = Array.Empty<string>();
        double[] stepNum = Array.Empty<double>();
        double[] u1 = Array.Empty<double>();
        double[] u2 = Array.Empty<double>();
        double[] u3 = Array.Empty<double>();
        double[] r1 = Array.Empty<double>();
        double[] r2 = Array.Empty<double>();
        double[] r3 = Array.Empty<double>();
        Check(_sapModel.Results.JointDispl("ALL", (eItemTypeElm)2, ref count, ref obj, ref elm,
            ref loadCase, ref stepType, ref stepNum, ref u1, ref u2, ref u3, ref r1, ref r2, ref r3),
            "Results.JointDispl(ALL)");
        var rowCount = new[] { count, obj.Length, elm.Length, loadCase.Length, u2.Length }.Min();
        if (rowCount == 0)
            throw new ConnectorException($"No joint displacement results were returned for load case: {caseName}");
        var indices = Enumerable.Range(0, rowCount).ToArray();
        var maxIndex = indices.MaxBy(i => u2[i]);
        var minIndex = indices.MinBy(i => u2[i]);
        var absoluteIndex = indices.MaxBy(i => Math.Abs(u2[i]));
        object Row(int i) => new
        {
            point_object = obj[i],
            point_element = elm[i],
            load_case = loadCase[i],
            step_type = i < stepType.Length ? stepType[i] : string.Empty,
            step_number = i < stepNum.Length ? stepNum[i] : 0,
            global_y_displacement = u2[i]
        };
        return new
        {
            case_name = caseName,
            direction = "Global Y (U2)",
            displacement_units = "mm",
            result_count = rowCount,
            maximum_positive = Row(maxIndex),
            minimum_negative = Row(minIndex),
            maximum_absolute = Row(absoluteIndex),
            model_path = (string)_sapModel.GetModelFilename(true)
        };
    }
    public object GetJointReactions(
        string[] pointNames,
        string[] cases,
        string[] combinations,
        int maxResults)
    {
        EnsureConnected();
        PrepareOutputSelection(cases, combinations);
        var limit = ValidateLimit(maxResults);
        var rows = new List<object>();
        var truncated = false;

        foreach (var pointName in pointNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int count = 0;
            string[] obj = Array.Empty<string>();
            string[] elm = Array.Empty<string>();
            string[] loadCase = Array.Empty<string>();
            string[] stepType = Array.Empty<string>();
            double[] stepNum = Array.Empty<double>();
            double[] f1 = Array.Empty<double>();
            double[] f2 = Array.Empty<double>();
            double[] f3 = Array.Empty<double>();
            double[] m1 = Array.Empty<double>();
            double[] m2 = Array.Empty<double>();
            double[] m3 = Array.Empty<double>();

            Check((int)_sapModel!.Results.JointReact(
                    pointName, ObjectElm, ref count, ref obj, ref elm, ref loadCase,
                    ref stepType, ref stepNum, ref f1, ref f2, ref f3, ref m1, ref m2, ref m3),
                $"Results.JointReact({pointName})");

            for (var i = 0; i < count; i++)
            {
                if (rows.Count >= limit)
                {
                    truncated = true;
                    break;
                }

                rows.Add(new
                {
                    object_name = obj[i],
                    element_name = elm[i],
                    load_case = loadCase[i],
                    step_type = stepType[i],
                    step_number = stepNum[i],
                    f1 = f1[i],
                    f2 = f2[i],
                    f3 = f3[i],
                    m1 = m1[i],
                    m2 = m2[i],
                    m3 = m3[i]
                });
            }

            if (truncated)
                break;
        }

        return ResultEnvelope(rows, truncated);
    }

    public object GetFrameForces(
        string[] frameNames,
        string[] cases,
        string[] combinations,
        int maxResults)
    {
        EnsureConnected();
        PrepareOutputSelection(cases, combinations);
        var limit = ValidateLimit(maxResults);
        var rows = new List<object>();
        var truncated = false;

        foreach (var frameName in frameNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            int count = 0;
            string[] obj = Array.Empty<string>();
            double[] objSta = Array.Empty<double>();
            string[] elm = Array.Empty<string>();
            double[] elmSta = Array.Empty<double>();
            string[] loadCase = Array.Empty<string>();
            string[] stepType = Array.Empty<string>();
            double[] stepNum = Array.Empty<double>();
            double[] p = Array.Empty<double>();
            double[] v2 = Array.Empty<double>();
            double[] v3 = Array.Empty<double>();
            double[] t = Array.Empty<double>();
            double[] m2 = Array.Empty<double>();
            double[] m3 = Array.Empty<double>();

            Check((int)_sapModel!.Results.FrameForce(
                    frameName, ObjectElm, ref count, ref obj, ref objSta, ref elm, ref elmSta,
                    ref loadCase, ref stepType, ref stepNum, ref p, ref v2, ref v3, ref t,
                    ref m2, ref m3),
                $"Results.FrameForce({frameName})");

            for (var i = 0; i < count; i++)
            {
                if (rows.Count >= limit)
                {
                    truncated = true;
                    break;
                }

                rows.Add(new
                {
                    object_name = obj[i],
                    object_station = objSta[i],
                    element_name = elm[i],
                    element_station = elmSta[i],
                    load_case = loadCase[i],
                    step_type = stepType[i],
                    step_number = stepNum[i],
                    p = p[i],
                    v2 = v2[i],
                    v3 = v3[i],
                    t = t[i],
                    m2 = m2[i],
                    m3 = m3[i]
                });
            }

            if (truncated)
                break;
        }

        return ResultEnvelope(rows, truncated);
    }

    public object ListApiMethods(string? query, int maxResults)
    {
        var limit = ValidateLimit(maxResults);
        return GenericApiGateway.ListMethods(query, limit);
    }

    public object DescribeApiMethod(string interfaceName, string methodName) =>
        GenericApiGateway.DescribeMethod(interfaceName, methodName);

    public object InvokeApiMethod(
        string objectPath,
        string methodName,
        int overload,
        JsonElement parameters,
        string requestedOperationKind,
        bool confirmDestructive)
    {
        EnsureConnected();
        var requested = NormalizeOperationKind(requestedOperationKind);
        var inferred = GenericApiGateway.InferOperationKind(objectPath, methodName);
        var effective = OperationRank(requested) >= OperationRank(inferred) ? requested : inferred;
        var destructive = IsDestructiveGenericOperation(objectPath, methodName);
        if (destructive && !confirmDestructive)
            throw new ConnectorException(
                $"{objectPath}.{methodName} can replace, delete, import, save, initialize, or close ETABS state. Set confirm_destructive=true only when the task explicitly authorizes it.");
        ValidateGenericPaths(parameters);
        var wasLocked = (bool)_sapModel!.GetModelIsLocked();
        if (effective != "read")
            RequireWriteReady();
        if (effective == "analysis" && !_settings.AllowAnalysis)
            throw new ConnectorException("Analysis is disabled. Set ETABS_CONNECTOR_ALLOW_ANALYSIS=true.");
        var unlockedAutomatically = wasLocked && !(bool)_sapModel.GetModelIsLocked();
        var result = GenericApiGateway.Invoke(
            _application, _sapModel, objectPath, methodName, overload, parameters, _settings.MaximumResultRows);
        return new
        {
            requested_operation_kind = requested,
            inferred_operation_kind = inferred,
            effective_operation_kind = effective,
            unlocked_automatically = unlockedAutomatically,
            destructive_operation_confirmed = destructive && confirmDestructive,
            result
        };
    }

    private void ValidateGenericPaths(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in parameters.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                continue;
            if (!property.Name.Contains("file", StringComparison.OrdinalIgnoreCase)
                && !property.Name.Contains("path", StringComparison.OrdinalIgnoreCase))
                continue;
            var path = property.Value.GetString();
            if (!string.IsNullOrWhiteSpace(path))
                _settings.RequireAllowedFileSystemPath(path, createParent: false);
        }
    }

    private static bool IsDestructiveGenericOperation(string objectPath, string methodName) =>
        objectPath.Equals("Application", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("New", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("Initialize", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("Open", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("Save", StringComparison.OrdinalIgnoreCase)
        || methodName.StartsWith("Import", StringComparison.OrdinalIgnoreCase)
        || methodName.Contains("ApplicationExit", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOperationKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "read" => "read",
        "write" => "write",
        "analysis" => "analysis",
        _ => throw new ConnectorException("operation_kind must be read, write, or analysis.")
    };

    private static int OperationRank(string value) => value switch
    {
        "read" => 0,
        "write" => 1,
        "analysis" => 2,
        _ => 1
    };
    public void Dispose()
    {
        // Detach only. Never close ETABS automatically because the user may
        // need to inspect or save the active working copy.
        // RCWs are left to normal runtime lifetime management.
        _sapModel = null;
        _application = null;
        _helper = null;
    }

    private void PrepareOutputSelection(string[] cases, string[] combinations)
    {
        if (cases.Length == 0 && combinations.Length == 0)
            throw new ConnectorException("Select at least one load case or combination.");

        Check((int)_sapModel!.Results.Setup.DeselectAllCasesAndCombosForOutput(),
            "Results.Setup.DeselectAllCasesAndCombosForOutput");
        foreach (var name in cases.Distinct(StringComparer.OrdinalIgnoreCase))
            Check((int)_sapModel.Results.Setup.SetCaseSelectedForOutput(name),
                $"Select output case {name}");
        foreach (var name in combinations.Distinct(StringComparer.OrdinalIgnoreCase))
            Check((int)_sapModel.Results.Setup.SetComboSelectedForOutput(name),
                $"Select output combination {name}");
    }

    private object ResultEnvelope(List<object> rows, bool truncated) => new
    {
        present_units = EtabsUnits.NameOf(Convert.ToInt32(_sapModel!.GetPresentUnits())),
        row_count = rows.Count,
        truncated,
        rows
    };

    private int ValidateLimit(int requested)
    {
        if (requested <= 0)
            return Math.Min(2_000, _settings.MaximumResultRows);
        return Math.Min(requested, _settings.MaximumResultRows);
    }

    private void RequireWriteReady()
    {
        EnsureWriteEnabled();
        EnsureConnected();
        if ((bool)_sapModel!.GetModelIsLocked())
        {
            Check((int)_sapModel.SetModelIsLocked(false), "SetModelIsLocked(false)");
            if ((bool)_sapModel.GetModelIsLocked())
                throw new ConnectorException("ETABS did not unlock the model for the authorized operation.");
        }
    }

    private bool IsWorkingCopyActive(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(_workingCopyPath) || string.IsNullOrWhiteSpace(modelPath))
            return false;
        var active = Path.GetFullPath(modelPath);
        var working = Path.GetFullPath(_workingCopyPath);
        if (active.Equals(working, StringComparison.OrdinalIgnoreCase))
            return true;
        return Path.GetExtension(active).Equals(".$et", StringComparison.OrdinalIgnoreCase)
            && Path.ChangeExtension(active, ".edb").Equals(working, StringComparison.OrdinalIgnoreCase)
            && File.Exists(working);
    }

    private void TryAdoptActiveWorkingCopy()
    {
        var active = (string)_sapModel!.GetModelFilename(true);
        if (string.IsNullOrWhiteSpace(active))
            return;
        var fullActive = Path.GetFullPath(active);
        var candidate = Path.GetExtension(fullActive).Equals(".$et", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(fullActive, ".edb")
            : fullActive;
        try
        {
            var allowed = _settings.RequireAllowedPath(candidate, createParent: false);
            if (File.Exists(allowed))
                _workingCopyPath = allowed;
        }
        catch (ConnectorException)
        {
            // Active model is outside authorised roots and cannot be adopted for writes.
        }
    }

    private void EnsureWriteEnabled()
    {
        if (!_settings.AllowWrites)
            throw new ConnectorException(
                "Write operations are disabled. Set ETABS_CONNECTOR_ALLOW_WRITES=true.");
    }

    private void EnsureConnected()
    {
        if (_sapModel is null)
            throw new ConnectorException("Not connected to ETABS. Call connect first.");
    }

    private static string[] GetNameList(object apiObject, string operation)
    {
        int count = 0;
        string[] names = Array.Empty<string>();
        var returnCode = apiObject switch
        {
            cLoadCases value => value.GetNameList(ref count, ref names),
            cCombo value => value.GetNameList(ref count, ref names),
            cPointObj value => value.GetNameList(ref count, ref names),
            cFrameObj value => value.GetNameList(ref count, ref names),
            cAreaObj value => value.GetNameList(ref count, ref names),
            cPropFrame value => value.GetNameList(ref count, ref names),
            cPropArea value => value.GetNameList(ref count, ref names),
            _ => throw new ConnectorException($"Unsupported typed ETABS name-list interface: {apiObject.GetType().FullName}")
        };
        Check(returnCode, operation);
        return names.Take(count).ToArray();
    }

    private object GetStoreyInfo()
    {
        int count = 0;
        string[] names = Array.Empty<string>();
        double[] elevations = Array.Empty<double>();
        double[] heights = Array.Empty<double>();
        bool[] isMaster = Array.Empty<bool>();
        string[] similarTo = Array.Empty<string>();
        bool[] spliceAbove = Array.Empty<bool>();
        double[] spliceHeight = Array.Empty<double>();
        Check(_sapModel!.Story.GetStories(ref count, ref names, ref elevations, ref heights,
            ref isMaster, ref similarTo, ref spliceAbove, ref spliceHeight), "Story.GetStories");
        var rowCount = new[] { names.Length, elevations.Length, heights.Length }.Min();
        var rows = Enumerable.Range(0, rowCount).Select(i => new
        {
            name = names[i],
            elevation = elevations[i],
            height = heights[i],
            is_master = i < isMaster.Length && isMaster[i],
            similar_to = i < similarTo.Length ? similarTo[i] : string.Empty
        }).ToArray();
        return new { count, rows };
    }

    private object GetAnalysisStatus()
    {
        int count = 0;
        string[] names = Array.Empty<string>();
        int[] statuses = Array.Empty<int>();
        Check(_sapModel!.Analyze.GetCaseStatus(ref count, ref names, ref statuses),
            "Analyze.GetCaseStatus");
        var rowCount = Math.Min(count, Math.Min(names.Length, statuses.Length));
        var rows = Enumerable.Range(0, rowCount).Select(i => new
        {
            case_name = names[i],
            status_code = statuses[i]
        }).ToArray();
        return new { count, rows };
    }

    private static int SafeCount(Func<int> count)
    {
        try
        {
            return count();
        }
        catch
        {
            return -1;
        }
    }

    private object ChangeResult(string operation, string objectName, string before, string after) =>
        new
        {
            operation,
            object_name = objectName,
            before,
            after,
            verified = true,
            working_copy_path = _workingCopyPath
        };

    private static void Check(int returnCode, string operation)
    {
        if (returnCode != 0)
            throw new ConnectorException(
                $"CSI API call failed: {operation}, return code {returnCode}.");
    }
}
