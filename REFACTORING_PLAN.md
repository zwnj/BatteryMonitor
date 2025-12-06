# BatteryMonitor3 プロジェクト改善計画 (Refactoring Plan)

## 1. プロジェクト概要

WPF (.NET 8) を使用して、Windowsのバッテリー詳細情報を表示するデスクトップアプリケーション。

## 2. 現状の問題点と解決策

現在の実装は、Win32 API (P/Invoke) を利用しているが、取得できる情報に限りがあり、複数の問題点を抱えている。根本的な原因はデータ取得方法にあるため、**WMI (Windows Management Instrumentation)** へ移行することで解決を図る。

| No. | 問題点 | 詳細 | 解決策 |
|:---:|:---|:---|:---|
| 1 | **電圧が不正確** | `CallNtPowerInformation` APIが電圧を返さないため、消費電力から**推定した不正確な値**を表示している。 | WMI (`root\wmi:BatteryStatus` の `Voltage` プロパティ) を利用して正確な値 (mV) を取得する。 |
| 2 | **健康状態が不正確** | 「設計容量」が取得できず、「現在の最大充電容量」で代用しているため、バッテリーの劣化が正しく計算されない。 | WMI (`root\wmi:BatteryStaticData` の `DesignedCapacity` プロパティ) を利用して「設計容量」を取得し、正確な健康状態を計算する。 |
| 3 | **MVVMパターンの違反** | データ加工や計算ロジックの大部分が `MainWindow.xaml.cs` (View) に実装されている。 | `BatteryViewModel.cs` (ViewModel) にロジックを移し、Viewはデータの表示に専念させる。 |
| 4 | **情報項目の変更** | UIに「温度(Temperature)」の表示領域があるが、ユーザーの要求により「サイクルカウント」へ変更する。 | WMI (`BatteryCycleCount` クラス) から「サイクルカウント」を取得し、UIへ表示する。 |
| 5 | **ファイル名のタイポ** | `BatteryService.cs.cs` というファイル名になっている。 | `BatteryService.cs` にリネームする。 |

## 3. 改善タスクリスト (進捗管理)

- [x] **1. 準備**
    - [x] `BatteryService.cs.cs` を `BatteryService.cs` にリネームする。
    - [x] `System.Management` NuGetパッケージをプロジェクトに追加する (WMI利用に必要)。
- [x] **2. データ取得層の改修 (`BatteryService.cs`)**
    - [x] WMIを利用して電圧、設計容量、現在の最大充電容量などを取得するロジックに書き換える。
- [x] **3. ViewModelの改修 (`BatteryViewModel.cs`)**
    - [x] `MainWindow.xaml.cs` からデータ処理ロジックをViewModelへ移動する。
    - [x] WMIから取得した正確なデータを使って各プロパティを更新する処理を実装する。
- [x] **4. Viewの整理 (`MainWindow.xaml.cs`)**
    - [x] ViewModelのロジックを呼び出すだけのシンプルな形に修正する。
- [x] **5. サイクルカウント情報の追加**
    - [x] `BatteryService.cs`: `BatteryInfo` structに `CycleCount` プロパティを追加し、WMIから値を取得するロジックを実装する。
    - [x] `BatteryViewModel.cs`: `Temperature` プロパティを `CycleCount` プロパティに変更し、UI表示用に整形する。
    - [x] `MainWindow.xaml`: UIの「TEMPERATURE」表示を「CYCLE COUNT」に変更し、バインディングも更新する。
- [ ] **6. 最終確認**
    - [ ] UIが全ての情報を正しく表示できることを確認する。