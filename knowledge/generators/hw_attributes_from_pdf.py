"""Build knowledge/api/hw-attributes-<ver>.json from Siemens' "TIA Portal Openness Hardware parameters.pdf".

Usage:  pdftotext -layout "TIA Portal Openness Hardware parameters.pdf" hw.txt
        python hw_attributes_from_pdf.py hw.txt V21 out.json [fallback.json]

Every table page starts with  "TIA Portal Openness ... HW parameters: <family>"  and a column header
"Channel Property DataType Label EomAtom EomAtom Description"; the column starts are read from that header
because they move from page to page. Rows:
  "Module[ - <MLFB> - <version>]"         -> current module
  text starting in the Property column     -> attribute (name, type, label)
  text starting in the EomAtom column      -> enum value + name of the previous attribute
Output (same shape as V18): {name: {type, label, families, module_count, module_examples, enums}}.
A fallback json (older version) fills attributes the PDF does not cover (e.g. V21 PDF has no ET200SP / S7-1200); those get "source".
"""
import json, re, sys
from collections import OrderedDict

def parse(path):
    attrs = {}
    family, module, cols, last = None, None, None, None
    for raw in open(path, encoding="utf-8", errors="replace"):
        line = raw.rstrip("\n")
        if not line.strip():
            continue
        m = re.match(r"^TIA Portal Openness\s+HW parameters:\s*(.*?)\s*$", line)
        if m:
            family = m.group(1) or family
            cols = None
            continue
        if "Property" in line and "DataType" in line and "EomAtom" in line:
            cols = {k: line.index(k) for k in ("Property", "DataType", "Label", "EomAtom")}
            continue
        if cols is None or line.strip() in ("Range", "Value", "Range Value") or re.match(r"^\s*Range\s+Value\s*$", line):
            continue
        if re.match(r"^\s*\d+ / \d+\s*$", line) or line.startswith("TIA Portal Openness Hardware parameters") or line.startswith("System Manual"):
            continue
        mm = re.match(r"^Module(?: - (.*))?\s*$", line.strip())
        if mm and line.startswith("Module"):
            module = (mm.group(1) or "").strip() or None
            last = None
            continue
        indent = len(line) - len(line.lstrip())
        if re.match(r"^\s*Channel\s+\d", line) and indent < cols["Property"] - 2:
            continue
        if abs(indent - cols["EomAtom"]) <= 3 and last is not None:
            parts = line.strip().split(None, 1)
            if len(parts) == 2 and re.match(r"^-?\d+$", parts[0]):
                last.setdefault("enums", OrderedDict())[parts[0]] = parts[1].strip()
            continue
        if abs(indent - cols["Property"]) <= 3:
            name = line[cols["Property"]:cols["DataType"]].strip() or line.strip().split()[0]
            name = name.split()[0] if name else name
            dtype = line[cols["DataType"]:cols["Label"]].strip().split()
            label = line[cols["Label"]:cols["EomAtom"]].strip() if len(line) > cols["Label"] else ""
            if not name or not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", name):
                last = None
                continue
            a = attrs.setdefault(name, {"type": dtype[0] if dtype else None, "label": None, "families": [], "modules": []})
            if not a["type"] and dtype:
                a["type"] = dtype[0]
            if label and not a["label"] and not label.startswith("*"):
                a["label"] = label
            if family and family not in a["families"]:
                a["families"].append(family)
            if module and module not in a["modules"]:
                a["modules"].append(module)
            last = a
            continue
    out = OrderedDict()
    for k in sorted(attrs):
        a = attrs[k]
        e = OrderedDict([("type", a["type"]), ("label", a["label"] or ""), ("families", a["families"]),
                         ("module_count", len(a["modules"])), ("module_examples", a["modules"][:5])])
        if a.get("enums"):
            e["enums"] = a["enums"]
        out[k] = e
    return out

if __name__ == "__main__":
    txt, ver, dst = sys.argv[1], sys.argv[2], sys.argv[3]
    res = parse(txt)
    own = len(res)
    if len(sys.argv) > 4:
        old = json.load(open(sys.argv[4], encoding="utf-8"))
        for k, v in old.items():
            if k not in res:
                v = OrderedDict(v); v["source"] = "older manual"
                res[k] = v
            else:
                # keep families/enums the new manual lacks (e.g. ET200SP use of a shared attribute)
                fam = res[k]["families"]
                for f in v.get("families", []):
                    if f not in fam: fam.append(f)
                if v.get("enums") and not res[k].get("enums"):
                    res[k]["enums"] = v["enums"]
        res = OrderedDict(sorted(res.items()))
    json.dump(res, open(dst, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    print(ver, "attributes from manual:", own, "total with fallback:", len(res))
