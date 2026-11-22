using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.MasterCopies;

namespace TiaAssistant
{
    // Screens through library master copies. Unlike XML export/import this keeps objects that V18 Openness
    // cannot serialize for classic panels (e.g. the Alarm view, verified on KTP700 Basic and TP900 Comfort).
    public static partial class Tia
    {
        static Screen FindScreen(HmiTarget h, string name)
        {
            List<KeyValuePair<string, Screen>> screens = new List<KeyValuePair<string, Screen>>();
            CollectScreens(h.ScreenFolder, "", screens);
            foreach (KeyValuePair<string, Screen> kv in screens)
                if (string.Equals(kv.Value.Name, name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return null;
        }

        static void CollectTemplateNames(ScreenTemplateFolder f, List<string> names)
        {
            foreach (ScreenTemplate t in f.ScreenTemplates) names.Add(t.Name);
            foreach (ScreenTemplateUserFolder sub in f.Folders) CollectTemplateNames(sub, names);
        }

        // "" when no template; the LinkList element otherwise. Refuses unknown templates (a dangling link can break the import).
        static string TemplateLink(HmiTarget h, string template)
        {
            if (string.IsNullOrEmpty(template)) return "";
            List<string> names = new List<string>();
            CollectTemplateNames(h.ScreenTemplateFolder, names);
            foreach (string n in names)
                if (string.Equals(n, template, StringComparison.OrdinalIgnoreCase))
                    return "<LinkList><Template TargetID=\"@OpenLink\"><Name>" + System.Security.SecurityElement.Escape(n) + "</Name></Template></LinkList>";
            throw new InvalidOperationException("Screen template '" + template + "' does not exist. Known: " + string.Join(", ", names.ToArray()) + ". Nothing was changed.");
        }

        static int MaxScreenNumber(HmiTarget h)
        {
            int n = 0;
            List<KeyValuePair<string, Screen>> screens = new List<KeyValuePair<string, Screen>>();
            CollectScreens(h.ScreenFolder, "", screens);
            foreach (KeyValuePair<string, Screen> kv in screens)
            {
                Screen sc = kv.Value;
                string sx = ExportToText(delegate(FileInfo f) { sc.Export(f, ExportOptions.WithDefaults); });
                Match m = Regex.Match(sx, @"<Number>(\d+)</Number>");
                if (m.Success) n = Math.Max(n, int.Parse(m.Groups[1].Value));
            }
            return n;
        }

        public static object HmiScreenToLibrary(string station, string screen, string item)
        {
            HmiTarget h = HmiOf(station);
            Screen s = FindScreen(h, screen);
            if (s == null) throw new InvalidOperationException("No screen '" + screen + "' in '" + station + "'. Call hmi_tree.");
            MasterCopyFolder root = Proj().ProjectLibrary.MasterCopyFolder;
            MasterCopyFolder f = root.Folders.Find("TIA_Assistant") ?? root.Folders.Create("TIA_Assistant");
            if (string.IsNullOrEmpty(item)) item = screen;
            if (FindMasterCopy(root, item) != null)
                throw new InvalidOperationException("The project library already has a master copy '" + item + "'. Give another 'item' name. Nothing was changed.");
            MasterCopy m = f.MasterCopies.Create(s);
            string note = null;
            if (!string.Equals(m.Name, item, StringComparison.Ordinal)) { try { m.Name = item; } catch (Exception e) { note = "Could not rename master copy: " + e.Message; } }
            return U.Obj("library", "project", "folder", "TIA_Assistant", "item", m.Name, "from_screen", s.Name, "station", station, "note", note);
        }

        public static object HmiScreenFromLibrary(string station, string library, string item, string name)
        {
            HmiTarget h = HmiOf(station);
            ArrayList notes = new ArrayList();
            MasterCopyFolder mcf;
            if (string.IsNullOrEmpty(library) || string.Equals(library, "project", StringComparison.OrdinalIgnoreCase)) mcf = Proj().ProjectLibrary.MasterCopyFolder;
            else
            {
                GlobalLibrary g = OpenGlobal(library, notes);
                if (g == null) throw new InvalidOperationException("Global library '" + library + "' not found. Call lib_list.");
                mcf = g.MasterCopyFolder;
            }
            MasterCopy m = FindMasterCopy(mcf, item);
            if (m == null) throw new InvalidOperationException("Master copy '" + item + "' not found. Call lib_list.");
            if (!string.IsNullOrEmpty(name) && FindScreen(h, name) != null)
                throw new InvalidOperationException("Screen '" + name + "' already exists in '" + station + "'. Nothing was changed.");
            Screen s = h.ScreenFolder.Screens.CreateFrom(m);
            // Screen name and number are read-only in V18 Openness: the copy keeps the source screen's name
            // (TIA appends _n if taken). Name the template screen as the new screen should be called.
            if (!string.IsNullOrEmpty(name) && !string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                notes.Add("Screen is named '" + s.Name + "' (V18 Openness cannot rename screens). Rename it in TIA, or save a template screen that already has the wanted name.");
            int number = 0;
            try { Match nm = Regex.Match(ExportToText(delegate(FileInfo f) { s.Export(f, ExportOptions.WithDefaults); }), @"<Number>(\d+)</Number>"); if (nm.Success) number = int.Parse(nm.Groups[1].Value); } catch { }
            return U.Obj("station", station, "screen", s.Name, "number", number, "from", item, "notes", notes);
        }
    }
}
