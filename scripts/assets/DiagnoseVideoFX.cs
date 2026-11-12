using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint
{
    public void FromVegas(Vegas vegas)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VideoFX_diagnostic.txt");
        List<string> lines = new List<string>();

        AddTree(lines, "VideoFX", vegas.VideoFX);
        AddTree(lines, "VideoFXPackages", vegas.VideoFXPackages);
        AddTree(lines, "VideoCompositeFX", vegas.VideoCompositeFX);
        AddTree(lines, "PlugIns", vegas.PlugIns);
        AddTree(lines, "Transitions", vegas.Transitions);
        AddTree(lines, "Generators", vegas.Generators);

        lines.Add("");
        lines.Add("=== Lines containing 'Stabil' ===");
        foreach (string line in lines.ToArray())
        {
            if (line.IndexOf("Stabil", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                lines.Add(">> " + line);
            }
        }

        try
        {
            File.WriteAllLines(path, lines.ToArray());
            MessageBox.Show("Diagnostic written to:\n" + path + "\n\nSearch it for 'Stabil' and paste the matching lines here.", "Diagnose Video FX");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not write diagnostic file: " + ex.Message, "Diagnose Video FX");
        }
    }

    private void AddTree(List<string> lines, string title, PlugInNode root)
    {
        lines.Add("");
        lines.Add("=== " + title + " ===");
        try { CollectNodes(root, lines, 0); } catch (Exception ex) { lines.Add("(error enumerating: " + ex.Message + ")"); }
    }

    private void CollectNodes(PlugInNode node, List<string> lines, int depth)
    {
        if (node == null) return;

        string name = "", group = "", uniqueID = "";
        bool isContainer = false, isOFX = false;
        try { name = node.Name; } catch { }
        try { group = node.Group; } catch { }
        try { uniqueID = node.UniqueID; } catch { }
        try { isContainer = node.IsContainer; } catch { }
        try { isOFX = node.IsOFX; } catch { }

        string indent = new string(' ', depth * 2);
        string marker = (name + " " + group + " " + uniqueID).IndexOf("Stabil", StringComparison.OrdinalIgnoreCase) >= 0 ? "  <<< STABIL" : "";
        lines.Add(indent + "Name=" + name + " | Group=" + group + " | UniqueID=" + uniqueID + " | IsContainer=" + isContainer + " | IsOFX=" + isOFX + marker);

        if (isContainer)
        {
            int count = 0;
            try { count = node.Count; } catch { return; }
            for (int i = 0; i < count; i++)
            {
                PlugInNode child = null;
                try { child = node.GetChild(i); } catch { continue; }
                if (child != null) CollectNodes(child, lines, depth + 1);
            }
        }
    }
}
