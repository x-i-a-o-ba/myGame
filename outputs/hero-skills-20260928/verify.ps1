$ErrorActionPreference='Stop'
$root='D:/UnityProject/myGame/outputs/hero-skills-20260928'
$excel=New-Object -ComObject Excel.Application
$excel.Visible=$false
$excel.DisplayAlerts=$false
try {
 $book=$excel.Workbooks.Open("$root/英雄与技能配置.xlsx",0,$true)
 $excel.CalculateFullRebuild()
 $q=$book.Worksheets.Item('ID查询')
 foreach($pair in @(@(1000,'技能'),@(2000,'英雄'),@(3000,'怪物'),@(3999,'未找到'),@(9999,'编号越界或非整数'))){
  $q.Range('B4').Value2=[double]$pair[0];$excel.CalculateFullRebuild()
  if($q.Range('B7').Text -ne $pair[1]){throw "查询失败 $($pair[0])"}
 }
 $q.Range('B4').Value2=[double]1003;$excel.CalculateFullRebuild()
 $ranges=@{'ID查询'='A1:B23';'英雄'='A5:J7';'技能'='A5:M13';'怪物'='A5:J6';'英雄技能装配'='A5:G13';'技能阶段'='A5:I10'}
 foreach($sheet in $book.Worksheets){
  foreach($cell in $sheet.UsedRange.Cells){if($cell.Text -match '^#(REF!|DIV/0!|VALUE!|NAME\?|N/A|NUM!|NULL!)$'){throw "公式错误 $($sheet.Name) $($cell.Address())"}}
  $sheet.PageSetup.Orientation=2;$sheet.PageSetup.PaperSize=8;$sheet.PageSetup.Zoom=$false
  $sheet.PageSetup.FitToPagesWide=1;$sheet.PageSetup.FitToPagesTall=1
  $sheet.PageSetup.PrintArea=$ranges[$sheet.Name]
  $sheet.ExportAsFixedFormat(0,"$root/$($sheet.Name).pdf")
 }
 Write-Output 'EXCEL_NATIVE_PASS: 3 categories and missing lookup; no formula errors; 6 sheets exported.'
} finally {
 if($book){$book.Close($false)}
 $excel.Quit()
 [Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel)|Out-Null
}
