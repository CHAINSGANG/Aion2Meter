# Архитектура

```text
Npcap
  └─ TCP payloads
      └─ GameStreamReassembler
          └─ DpsPacketParser
              ├─ damage / heal / identity / target events
              └─ DataRepository
                  └─ DpsCalculator
                      └─ MeterRuntime snapshot
                          └─ MeterViewModel
                              └─ WPF overlay
```

## Границы

`AionFlex.Core` содержит DTO, enum, боевые модели и интерфейсы. Он не знает о UI или сетевом драйвере.

`AionFlex.Infrastructure` скомпилирован по явному whitelist файлов. В сборку попадают только:

- `NpcapCaptureService` и watchdog;
- `GameStreamReassembler`;
- диагностика протокола;
- `PacketParsing/Dps`.

Код Auth, Licensing, Telemetry, Updating, Settings, Sessions, Armory и WindowManagement не компилируется и не попадает в итоговые DLL.

`AionFlex.Application` содержит только пять боевых компонентов: `DataRepository`, `DpsCalculator`, `SummonOwnerResolver`, `SkillRepository` и `MobRepository`.

`Aion2Meter` владеет lifecycle, связывает events, преобразует боевой snapshot в UI-модель и не знает деталей бинарного протокола.

## Режимы запуска

Обычный запус загружает JSON-справочники, открывает Npcap devices и начинает захват. Аргумент `--demo` не запускает Npcap и генерирует детерминированный live snapshot для UI-разработки.

## Данные

В publish-каталог копируются только справочники, нужные текущему parser/calculator:

- skills, NPC skills, theostones;
- skill effect types и summon skills;
- mobs и NPC summons;
- class icons.

Пути к данным строятся от `AppContext.BaseDirectory`, поэтому exe можно запускать из любой рабочей папки.
