"""StartWise prototype: build a LAD FC (e.g. "Servo Motors" / "Asenkron Motors") with one FB-call network per drive.
Each pin is wired to a member of the drive's interface DB (Cmd.* inputs, Sts.* outputs); InOut/constant pins can be given
as a global symbol. Templates (document head, CompileUnit wrapper) come from TIA's own exports in work/.
Usage: python3 lad_drive_fc.py spec.json out.xml   (spec: see SPEC example at the bottom)"""
import json, re, sys
from xml.sax.saxutils import escape

def access(uid, parts):
    if len(parts) == 1 and parts[0].startswith("const:"):     # system / global constant, e.g. HW identifier
        return f'      <Access Scope="GlobalConstant" UId="{uid}">\n        <Constant Name="{escape(parts[0][6:])}" />\n      </Access>'
    comps = ''.join(f'<Component Name="{escape(p)}" />' for p in parts)
    return f'      <Access Scope="GlobalVariable" UId="{uid}">\n        <Symbol>{comps}</Symbol>\n      </Access>'

def network(net):
    uid = 21; parts = []; wires = []
    call = uid; uid += 1; inst = uid; uid += 1
    pins = '\n'.join(f'        <Parameter Name="{p["pin"]}" Section="{p["section"]}" Type="{p["type"]}" />' for p in net["pins"])
    parts.append(f'''      <Call UId="{call}">
      <CallInfo Name="{escape(net["fb"])}" BlockType="FB">
        <Instance Scope="GlobalVariable" UId="{inst}">
          <Component Name="{escape(net["instance"])}" />
        </Instance>
{pins}
      </CallInfo>
    </Call>''')
    def wire(inner):
        nonlocal uid
        wires.append(f'    <Wire UId="{uid}">\n{inner}\n    </Wire>'); uid += 1
    wire(f'      <Powerrail />\n      <NameCon UId="{call}" Name="en" />')
    for p in net["pins"]:
        src = p.get("to")
        if not src:
            oc = uid; uid += 1
            if p["section"] == "Input": wire(f'      <OpenCon UId="{oc}" />\n      <NameCon UId="{call}" Name="{p["pin"]}" />')
            else: wire(f'      <NameCon UId="{call}" Name="{p["pin"]}" />\n      <OpenCon UId="{oc}" />')
            continue
        a = uid; uid += 1
        parts.append(access(a, [src] if src.startswith("const:") else src.split('.')))
        if p["section"] == "Output": wire(f'      <NameCon UId="{call}" Name="{p["pin"]}" />\n      <IdentCon UId="{a}" />')
        else: wire(f'      <IdentCon UId="{a}" />\n      <NameCon UId="{call}" Name="{p["pin"]}" />')
    return ('<FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4">\n  <Parts>\n'
            + '\n'.join(parts) + '\n  </Parts>\n  <Wires>\n' + '\n'.join(wires) + '\n  </Wires>\n</FlgNet>')

def build(spec, main_template, fc_template):
    m = open(main_template, encoding='utf-8-sig').read()
    unit_t = [u.group(0) for u in re.finditer(r'<SW\.Blocks\.CompileUnit ID="[0-9A-F]+".*?</SW\.Blocks\.CompileUnit>', m, re.S)
              if '<FlgNet' in u.group(0)][0]
    f = open(fc_template, encoding='utf-8-sig').read()
    head = f[:f.index('<SW.Blocks.FC')]
    nid = 3; units = []
    for net in spec["networks"]:
        cu = re.sub(r'<FlgNet .*?</FlgNet>', lambda _: network(net), unit_t, flags=re.S)
        for old in re.findall(r'\bID="([0-9A-F]+)"', cu):
            cu = cu.replace(f'ID="{old}"', f'ID="__{nid:X}__"', 1); nid += 1
        cu = re.sub(r'ID="__([0-9A-F]+)__"', r'ID="\1"', cu)
        cu = re.sub(r'(CompositionName="Title">.*?<Text)( />|>[^<]*</Text>)', lambda mm: mm.group(1) + '>' + escape(net["title"]) + '</Text>', cu, count=1, flags=re.S)
        units.append(cu)
    return head + f'''<SW.Blocks.FC ID="0">
    <AttributeList>
      <AutoNumber>true</AutoNumber>
      <HeaderAuthor>StartWise</HeaderAuthor>
      <HeaderFamily />
      <HeaderName />
      <HeaderVersion>0.1</HeaderVersion>
      <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
  <Section Name="Input" />
  <Section Name="Output" />
  <Section Name="InOut" />
  <Section Name="Temp" />
  <Section Name="Constant" />
  <Section Name="Return">
    <Member Name="Ret_Val" Datatype="Void" Accessibility="Public" />
  </Section>
</Sections></Interface>
      <IsIECCheckEnabled>false</IsIECCheckEnabled>
      <MemoryLayout>Optimized</MemoryLayout>
      <Name>{escape(spec["fc"])}</Name>
      <Namespace />
      <ProgrammingLanguage>LAD</ProgrammingLanguage>
      <SetENOAutomatically>false</SetENOAutomatically>
      <UDABlockProperties />
      <UDAEnableTagReadback>false</UDAEnableTagReadback>
    </AttributeList>
    <ObjectList>
      <MultilingualText ID="1" CompositionName="Comment">
        <ObjectList>
          <MultilingualTextItem ID="2" CompositionName="Items">
            <AttributeList>
              <Culture>en-US</Culture>
              <Text>{escape(spec.get("comment", ""))}</Text>
            </AttributeList>
          </MultilingualTextItem>
        </ObjectList>
      </MultilingualText>
''' + '\n'.join(units) + f'''
      <MultilingualText ID="{nid:X}" CompositionName="Title">
        <ObjectList>
          <MultilingualTextItem ID="{nid+1:X}" CompositionName="Items">
            <AttributeList>
              <Culture>en-US</Culture>
              <Text>{escape(spec["fc"])}</Text>
            </AttributeList>
          </MultilingualTextItem>
        </ObjectList>
      </MultilingualText>
    </ObjectList>
  </SW.Blocks.FC>
</Document>
'''

# S210_AbsCnt pins: (pin, section, type, interface-DB member or None)
S210_PINS = [("Enable","Input","Bool","Cmd.Enable"),("Reset","Input","Bool","Cmd.Reset"),("Home","Input","Bool","Cmd.Home"),
 ("HomeMode","Input","Int","Cmd.HomeMode"),("HomePosition","Input","LReal","Cmd.HomePosition"),("StartPos","Input","Bool","Cmd.StartPos"),
 ("Position","Input","LReal","Cmd.Position"),("Velocity","Input","LReal","Cmd.Velocity"),("Direction","Input","Int","Cmd.Direction"),
 ("JogFwd","Input","Bool","Cmd.JogFwd"),("JogBwd","Input","Bool","Cmd.JogBwd"),("JogVelocity","Input","LReal","Cmd.JogVelocity"),
 ("Stop","Input","Bool","Cmd.Stop"),("StandStillVel","Input","LReal","Cmd.StandStillVel"),
 ("Powered","Output","Bool","Sts.Powered"),("Homed","Output","Bool","Sts.Homed"),("Busy","Output","Bool","Sts.Busy"),
 ("InPosition","Output","Bool","Sts.InPosition"),("StandStill","Output","Bool","Sts.StandStill"),("ActPos","Output","LReal","Sts.ActPos"),
 ("ActVel","Output","LReal","Sts.ActVel"),("Error","Output","Bool","Sts.Error"),("ErrorID","Output","Word","Sts.ErrorID")]

def servo_network(drive, to_db):
    pins = [{"pin": p, "section": s, "type": t, "to": f"{drive}_IO.{m}"} for p, s, t, m in S210_PINS]
    pins.append({"pin": "Axis", "section": "InOut", "type": "TO_PositioningAxis", "to": to_db})
    return {"title": f"{drive} - S210_AbsCnt (TO: {to_db}, arayuz: {drive}_IO)", "fb": "S210_AbsCnt", "instance": f"S210_AbsCnt_{drive}", "pins": pins}

# SINA_SPEED pins (DriveLib), telegram 20 default
SINA_PINS = [("EnableAxis","Input","Bool","Cmd.EnableAxis"),("AckError","Input","Bool","Cmd.AckError"),("SpeedSp","Input","Real","Cmd.SpeedSp"),
 ("RefSpeed","Input","Real","Cmd.RefSpeed"),("ConfigAxis","Input","Word","Cmd.ConfigAxis"),
 ("AxisEnabled","Output","Bool","Sts.AxisEnabled"),("Lockout","Output","Bool","Sts.Lockout"),("ActVelocity","Output","Real","Sts.ActVelocity"),
 ("Error","Output","Bool","Sts.Error"),("Status","Output","Word","Sts.Status"),("DiagId","Output","Word","Sts.DiagId")]

def async_network(drive, hwid):
    pins = [{"pin": p, "section": s, "type": t, "to": f"{drive}_IO.{m}"} for p, s, t, m in SINA_PINS]
    pins.insert(5, {"pin": "HWIDSTW", "section": "Input", "type": "HW_IO", "to": "const:" + hwid})
    pins.insert(6, {"pin": "HWIDZSW", "section": "Input", "type": "HW_IO", "to": "const:" + hwid})
    return {"title": f"{drive} - SINA_SPEED (HW: {hwid}, arayuz: {drive}_IO)", "fb": "SINA_SPEED", "instance": f"SINA_SPEED_{drive}", "pins": pins}

if __name__ == "__main__":
    spec = json.load(open(sys.argv[1], encoding='utf-8'))
    nets = []
    for n in spec["networks"]:
        k = n.get("kind")
        nets.append(servo_network(n["drive"], n["to_db"]) if k == "servo" else
                    async_network(n["drive"], n.get("hwid", n["drive"] + "~PROFINET_interface~Standard_telegram_20")) if k == "async" else n)
    spec["networks"] = nets
    open(sys.argv[2], 'w', encoding='utf-8').write(build(spec, sys.argv[3], sys.argv[4]))
    print("ok", len(nets), "network(s)")
