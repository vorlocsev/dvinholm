#requires -Version 5.1
# Dvinholm J2534 driver UI, loaded by Updater-Banner after Aurora theme.
# No network on refresh. Vendor downloads are manual, from official pages.
$driverScript=Join-Path $PSScriptRoot 'J2534-DriverManager.ps1'
function DriverText([string]$ru,[string]$en){
 if($script:launcherLanguage -eq 'EN'){return $en}
 return $ru
}
function Get-DriverInventory {
 if(-not(Test-Path -LiteralPath $driverScript -PathType Leaf)){
  throw 'J2534 driver manager is missing.'
 }
 $raw=(& $driverScript -Mode Probe |Out-String).Trim()
 if(-not $raw){throw 'Driver inventory did not return a result.'}
 return (ConvertFrom-Json -InputObject $raw -ErrorAction Stop)
}
function Driver-Install([string]$target,[Windows.Forms.Form]$owner,[Windows.Forms.Label]$status){
 if($script:child -or $script:vendor){return}
 $dialog=New-Object Windows.Forms.OpenFileDialog
 $dialog.Title=DriverText 'Выберите официальный подписанный установщик производителя' 'Select an official signed manufacturer installer'
 $dialog.Filter=DriverText 'Установщики производителя (*.exe;*.msi)|*.exe;*.msi' 'Manufacturer setup (*.exe;*.msi)|*.exe;*.msi'
 $dialog.CheckFileExists=$true;$dialog.Multiselect=$false
 if($dialog.ShowDialog($owner) -ne [Windows.Forms.DialogResult]::OK){return}
 $chosen=$dialog.FileName
 try {
  $json=(& $driverScript -Mode Verify -Target $target -InstallerPath $chosen |Out-String).Trim()
  if(-not $json){throw 'No signature inspection result.'}
  $checked=ConvertFrom-Json -InputObject $json -ErrorAction Stop
  if(-not $checked.Publisher){throw 'No manufacturer publisher.'}
 }catch{
  [void][Windows.Forms.MessageBox]::Show($owner,
   (DriverText 'Пакет не прошёл проверку подписи производителя. Установка отменена.' 'The installer did not pass manufacturer signature verification. Installation cancelled.'),
   'Dvinholm J2534',[Windows.Forms.MessageBoxButtons]::OK,
   [Windows.Forms.MessageBoxIcon]::Warning)
  return
 }
 $message=(DriverText 'Издатель: ' 'Publisher: ')+$checked.Publisher+[Environment]::NewLine+
   (DriverText 'Файл: ' 'File: ')+[IO.Path]::GetFileName($chosen)+[Environment]::NewLine+
   (DriverText 'Запустить установку? Windows может запросить права администратора.' 'Run installer? Windows may require administrator approval.')
 if([Windows.Forms.MessageBox]::Show($owner,$message,(DriverText 'Подтверждение установки J2534' 'Confirm J2534 installation'),
    [Windows.Forms.MessageBoxButtons]::YesNo,[Windows.Forms.MessageBoxIcon]::Question) -ne
   [Windows.Forms.DialogResult]::Yes){return}
 # A separate visible Windows PowerShell process delegates to the signed
 # vendor setup. No silent switches, no direct DLL loading or forced reboot.
 $command='-NoLogo -NoProfile -ExecutionPolicy Bypass -File "'+$driverScript+
  '" -Mode Install -Approved -Target '+$target+' -InstallerPath "'+$chosen+'"'
 try {
  $script:vendor=Start-Process -FilePath $powershell -ArgumentList $command -PassThru
  $script:driverInstallerPending=$true
  $script:driverResultControl=$status
  $status.Text=DriverText 'Установка открыта. Следуйте инструкциям производителя.' 'Installer opened. Follow the manufacturer instructions.'
  Update-ActionAvailability
 }catch{
  $status.Text=DriverText 'Не удалось запустить установщик производителя.' 'Could not start the manufacturer installer.'
  Update-ActionAvailability
 }
}
function Show-J2534Drivers {
 if($script:child -or $script:vendor){return}
 $dlg=New-Object Windows.Forms.Form
 $dlg.Text=DriverText 'Dvinholm | Драйверы адаптеров' 'Dvinholm | Adapter drivers'
 $dlg.ClientSize=New-Object Drawing.Size(900,565)
 $dlg.StartPosition='CenterParent'
 $dlg.FormBorderStyle=[Windows.Forms.FormBorderStyle]::FixedDialog
 $dlg.MaximizeBox=$false
 $dlg.BackColor=[Drawing.Color]::FromArgb(10,24,43)
 $dlg.ForeColor=[Drawing.Color]::White
 $dlg.Font=New-Object Drawing.Font('Segoe UI',10)
 function DriverLabel([string]$value,[int]$y,[int]$height){
  $l=New-Object Windows.Forms.Label
  $l.Text=$value;$l.SetBounds(20,$y,860,$height)
  $l.ForeColor=[Drawing.Color]::FromArgb(201,231,248)
  $dlg.Controls.Add($l)
  return $l
 }
 $null=DriverLabel (DriverText 'ДРАЙВЕРЫ АДАПТЕРОВ | WINDOWS X86' 'ADAPTER DRIVERS | WINDOWS X86') 14 30
 $dice=DriverLabel (DriverText 'Volvo DiCE: USB ещё не проверен' 'Volvo DiCE: USB not checked') 54 40
 $mongoose=DriverLabel (DriverText 'MongoosePro JLR: J2534 ещё не проверен' 'MongoosePro JLR: J2534 not checked') 99 40
 $notice=DriverLabel (DriverText 'DiCE работает с VIDA отдельно от J2534. Поддержка DiCE в Dvinholm не подтверждена.' 'DiCE and J2534 are different VIDA interfaces. Dvinholm DiCE support is not verified.') 144 49
 $notice.ForeColor=[Drawing.Color]::FromArgb(245,191,119)
 $list=New-Object Windows.Forms.ListView
 $list.SetBounds(20,200,858,207)
 $list.View=[Windows.Forms.View]::Details
 $list.FullRowSelect=$true;$list.GridLines=$true
 $list.BackColor=[Drawing.Color]::FromArgb(22,43,64)
 $list.ForeColor=[Drawing.Color]::White
 [void]$list.Columns.Add((DriverText 'Зарегистрированный J2534' 'Registered J2534 adapter'),285)
 [void]$list.Columns.Add('API',65)
 [void]$list.Columns.Add((DriverText 'Проверка x86 DLL' 'x86 DLL check'),230)
 [void]$list.Columns.Add((DriverText 'Путь' 'Path'),260)
 $dlg.Controls.Add($list)
 function DriverButton([string]$text,[int]$x,[int]$y,[int]$width){
  $b=New-Object Windows.Forms.Button
  $b.Text=$text;$b.SetBounds($x,$y,$width,37)
  $b.BackColor=[Drawing.Color]::FromArgb(22,74,121)
  $b.ForeColor=[Drawing.Color]::White
  $b.FlatStyle=[Windows.Forms.FlatStyle]::Flat
  $dlg.Controls.Add($b)
  return $b
 }
 $probe=DriverButton (DriverText 'Проверить' 'Scan') 20 426 188
 $diceLink=DriverButton (DriverText 'Volvo: инструкции' 'Volvo: instructions') 219 426 206
 $mongooseLink=DriverButton (DriverText 'Mongoose: драйверы' 'Mongoose: drivers') 437 426 217
 $close=DriverButton (DriverText 'Закрыть' 'Close') 670 426 207
 $diceInstall=DriverButton (DriverText 'Установить Volvo DiCE...' 'Install Volvo DiCE...') 20 477 407
 $mongooseInstall=DriverButton (DriverText 'Установить Mongoose JLR...' 'Install Mongoose JLR...') 443 477 435
 $result=DriverLabel (DriverText 'Проверка не выполнена. Работа с адаптерами не тестируется.' 'Not checked. Hardware operation is not tested.') 529 27
 # Capture the dialog controls and inventory helper for the parent's
 # timer-driven post-install rescan. GetNewClosure has its own script scope.
 # A completed installer is NOT proof that J2534 communication works.
 $inventoryCommand=${function:Get-DriverInventory}
 $scan={
  if($dlg.IsDisposed){return}
  $list.Items.Clear()
  try{
   $data=& $inventoryCommand
   $dice.Text=(DriverText 'Volvo DiCE: ' 'Volvo DiCE: ')+[string]$data.Targets.VolvoDice.Status
   $mongoose.Text=(DriverText 'MongoosePro JLR: ' 'MongoosePro JLR: ')+[string]$data.Targets.MongooseJlr.Status
   foreach($device in @($data.Devices|Where-Object{$null -ne $_})){
    $entry=New-Object Windows.Forms.ListViewItem(([string]$device.Name+' / '+[string]$device.Vendor))
    [void]$entry.SubItems.Add([string]$device.Api)
    [void]$entry.SubItems.Add([string]$device.Status)
    [void]$entry.SubItems.Add([string]$device.DllPath)
    [void]$list.Items.Add($entry)
   }
   $result.Text=((DriverText 'Найдено записей J2534: ' 'J2534 entries found: ')+$list.Items.Count+
    (DriverText '. Обнаружение не подтверждает работоспособность оборудования.' '. Detection does not verify physical hardware.'))
  }catch{
   $result.Text=(DriverText 'Не удалось прочитать драйверы J2534. Изменения не внесены.' 'Could not enumerate J2534 drivers. No changes made.')
  }
 }.GetNewClosure()
 $script:driverRescan=$scan
 $probe.Add_Click($scan)
 $diceLink.Add_Click({Start-Process 'https://www.volvospecialtools.com/index.cfm?event=info.dice'})
 $mongooseLink.Add_Click({Start-Process 'https://opusivs.com/support/product-downloads/'})
 $diceInstall.Add_Click({Driver-Install 'VolvoDice' $dlg $result})
 $mongooseInstall.Add_Click({Driver-Install 'MongooseJlr' $dlg $result})
 $close.Add_Click({$dlg.Close()})
 try{& $scan;[void]$dlg.ShowDialog($form)}
 finally{
  $script:driverRescan=$null
  $script:driverResultControl=$null
  $dlg.Dispose()
 }
}
$navDrivers=AuroraNav '▣   Драйверы адаптеров' 325
$script:auroraNav+=@($navDrivers)
$navDrivers.Add_Click({
 SelectAuroraNav $navDrivers
 Show-J2534Drivers
})
