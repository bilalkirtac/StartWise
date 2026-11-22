# Senaryo: S210 servo + TO_PositioningAxis

Doğrulandı: 18.11.2026, TIA V18, test_MCP. S210 7 kW 6SL5310-1BE17-0xFx V6.1 (Servo_1) + PLC_1 (CPU 1511-1 PN, FW V2.9). Gösterimsiz öğrenildi; hardware+software derleme 0 hata.

## Adımlar
1. Sürücü: device_add (type_identifier katalogdan; S210 en fazla 7 kW). network_connect io_controller=<PLC>.
   Ana telegram varsayılan 105 (DSC, tork azaltma, 1 enkoder) — TO için doğru; drive_telegram ile kontrol.
2. TO sürümü CPU firmware'ine bağlı: object_inspect <CPU> filter=Firmware. FW V2.9 → TO V6.0 (V7.0 geçersiz).
3. TO oluştur: object_create path=.../Software/TechnologicalObjectGroup/TechnologicalObjects
   parameters={Name, OfSystemLibElement:"TO_PositioningAxis", OfSystemLibVersion:"V6.0"} (sürüm System.Version olmalı; araç çeviriyor).
4. Sürücüye bağla: object_invoke path=.../TechnologicalObjects[<ad>]/service:AxisHardwareConnectionProvider/ActorInterface
   action=Connect args={addressIn:<telegram giriş baytı×8>, addressOut:<çıkış baytı×8>, connectOption:"Default"}.
   (Yeni nesil Startdrive cihazında telegram ayrı DeviceItem değil; bit adresiyle bağlanır.) Enkoder (SensorInterface) aynı telegrama otomatik bağlanır.
   Kontrol: to_list → actor/sensors connected, _Actor.Interface.Telegram=105.
5. IRT topolojisi: telegram 105/DSC servoyu IRT senkron alanına alır → port topolojisi ŞART, yoksa derlemede
   "Topology error ... sync master/sync slave" ve aynı IO sistemindeki diğer cihazlar için "No update time can be calculated".
   Çözüm: object_invoke path=<PLC>/DeviceItems[PROFINET interface_1]/DeviceItems[Port_2]/service:NetworkPort action=ConnectToPort
   args={partnerPort:"station:<servo>/DeviceItems[<servo>]/DeviceItems[PROFINET interface]/DeviceItems[Port_1]/service:NetworkPort"}.
   Gerçek kablolamayı mühendisle doğrula (hangi port hangisine).
6. compile scope=all (teknoloji alarm metin listesi için cihaz derlemesi gerekir).

## Varsayılanlar
- DataAdaption=1: referans hız/tork sürücüden online okunur, offline değerler (3000/1500 rpm) yer tutucu.
- Sonraki adımlar (henüz yapılmadı): mekanik (LeadScrew, dişli), yazılım limitleri, referanslama, kullanıcının mutlak konumlama FB'si ile MC_POWER/HOME/MOVEABSOLUTE.

## Genel kontrol bloğu: S210_AbsCnt (18.11.2026, derleme 0 hata)
- SCL FB; VAR_IN_OUT Axis : TO_PositioningAxis → her eksen kendi instance'ı ile (ör. S210_AbsCnt_Servo_1), Axis := "<TO DB>".
- İçinde MC_POWER/RESET/HOME/MOVEABSOLUTE/MOVEJOG/HALT; SCL kaynakta {InstructionName := 'MC_xxx'; LibVersion := '<TO sürümü>'} (TO V6.0 → '6.0').
- Girişler: Enable, Reset, Home, HomeMode(=7), HomePosition, StartPos, Position, Velocity, Direction, JogFwd/JogBwd, JogVelocity, Stop, StandStillVel.
- Çıkışlar: Powered, Homed, Busy, InPosition, StandStill, ActPos, ActVel, Error, ErrorID.
- DİKKAT S7-1500: TO durumunda StatusBits YOK (o S7-1200). Durum StatusWord (DWord) bitlerinde: bit 5 = HomingDone (Siemens FAQ, Axis.StatusWord.%X5). Diğer bit numaraları doğrulanmadı → StandStill hız+komutlardan hesaplanıyor; hata = MC hataları OR Axis.ErrorWord <> 0.
- TO DB arayüzünü görmek: object_invoke .../TechnologicalObjects[<ad>] action=Export (WithDefaults) → StatusWord, StatusWord2, ErrorWord, ErrorDetail, ActualPosition, ActualVelocity, ...

## OB1'de çağırma (LAD, 18.11.2026, derleme 0 hata)
- OB1 dosyaya alınır: object_invoke .../BlockGroup/Blocks[Main] action=Export (WithDefaults) → work\Main_before.xml.
- Yeni CompileUnit eklenir (mevcut FB çağrı ağı kalıp): Call/CallInfo Name="S210_AbsCnt" BlockType="FB", Instance Component="S210_AbsCnt_Servo_1",
  her parametre <Parameter Name Section Type>. TO DB ataması: <Access Scope="GlobalVariable"><Symbol><Component Name="Servo_1"/></Symbol></Access>
  + Wire IdentCon(access) → NameCon(call, "Axis"); Axis parametresi Section="InOut" Type="TO_PositioningAxis". Diğer pinler OpenCon.
- ID'ler belgedeki en büyük ID'den devam; UId'ler ağ içinde 21'den.
- İçe aktar: object_invoke .../BlockGroup/Blocks action=Import importOptions=Override (önce project_save). Sonra compile.
