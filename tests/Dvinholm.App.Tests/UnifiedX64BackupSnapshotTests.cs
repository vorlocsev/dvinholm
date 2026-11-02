using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64BackupSnapshotTests
{
    private static void Run(Action<UnifiedX64RecoveryPlan,UnifiedX64StagedPackage> test)
    {
        var root = Path.Combine(Path.GetTempPath(),"dvinholm-snapshot-"+Guid.NewGuid().ToString("N"));
        var install=Path.Combine(root,"app");
        var stage=Path.Combine(root,"stage","pending-"+Guid.NewGuid().ToString("N"));
        var backup=Path.Combine(root,"backups");
        Directory.CreateDirectory(install); Directory.CreateDirectory(stage);
        var exe=new byte[1024]; exe[0]=77; exe[1]=90; exe[128]=80; exe[129]=69;
        BitConverter.GetBytes(128).CopyTo(exe,60);
        BitConverter.GetBytes((ushort)0x8664).CopyTo(exe,132);
        File.WriteAllBytes(Path.Combine(install,"Dvinholm.exe"),exe);
        File.WriteAllText(Path.Combine(install,"settings.json"),"{}");
        var zip=Path.Combine(stage,UnifiedX64ReleaseFeed.PackageAsset);
        File.WriteAllBytes(zip,new byte[]{1,2,3,4});
        var staged=new UnifiedX64StagedPackage(stage,zip,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))).ToLowerInvariant(),4,4,515);
        try {
            var plan=UnifiedX64RecoveryPreflight.Inspect(install,staged,backup);
            Assert.True(plan.CanPlanBackup);
            test(plan,staged);
        } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Fact]
    public void VerifiedSnapshotIsAtomicAndNonDestructive()=>Run((plan,staged)=>{
        var original=File.ReadAllBytes(Path.Combine(plan.InstallDirectory,"Dvinholm.exe"));
        var receipt=UnifiedX64BackupSnapshot.Create(plan,staged);
        Assert.Equal(2,receipt.Files); Assert.Equal(1026,receipt.Bytes);
        Assert.Equal(receipt,UnifiedX64BackupSnapshot.Verify(receipt.Directory));
        Assert.Equal(original,File.ReadAllBytes(Path.Combine(plan.InstallDirectory,"Dvinholm.exe")));
        Assert.Empty(Directory.EnumerateDirectories(
            Path.GetDirectoryName(plan.BackupDirectory)!,".snapshot-*"));
    });

    [Fact]
    public void SourceMayChangeAfterSnapshotButSnapshotRemainsValid()=>Run((plan,staged)=>{
        var receipt=UnifiedX64BackupSnapshot.Create(plan,staged);
        File.WriteAllText(Path.Combine(plan.InstallDirectory,"settings.json"),"changed");
        Assert.Equal(receipt,UnifiedX64BackupSnapshot.Verify(receipt.Directory));
    });

    [Fact]
    public void TamperedSnapshotFileFails()=>Run((plan,staged)=>{
        var receipt=UnifiedX64BackupSnapshot.Create(plan,staged);
        File.WriteAllText(Path.Combine(receipt.Directory,"payload","settings.json"),"XX");
        Assert.Throws<InvalidDataException>(()=>UnifiedX64BackupSnapshot.Verify(receipt.Directory));
    });

    [Fact]
    public void TamperedJournalFails()=>Run((plan,staged)=>{
        var receipt=UnifiedX64BackupSnapshot.Create(plan,staged);
        File.WriteAllText(Path.Combine(receipt.Directory,"snapshot-journal.json"),"{}");
        Assert.Throws<InvalidDataException>(()=>UnifiedX64BackupSnapshot.Verify(receipt.Directory));
    });

    [Fact]
    public void UndocumentedExtraFileFails()=>Run((plan,staged)=>{
        var receipt=UnifiedX64BackupSnapshot.Create(plan,staged);
        File.WriteAllText(Path.Combine(receipt.Directory,"payload","intruder.dat"),"1");
        Assert.Throws<InvalidDataException>(()=>UnifiedX64BackupSnapshot.Verify(receipt.Directory));
    });

    [Fact]
    public void StalePreflightBlocksBeforeBackup()=>Run((plan,staged)=>{
        var changed=plan with {FilesToBackUp=123};
        Assert.Throws<InvalidDataException>(()=>UnifiedX64BackupSnapshot.Create(changed,staged));
        Assert.False(Directory.Exists(Path.GetDirectoryName(plan.BackupDirectory)!));
    });

    [Fact]
    public void ExistingBackupCannotBeOverwritten()=>Run((plan,staged)=>{
        var receipt=UnifiedX64BackupSnapshot.Create(plan,staged);
        Assert.Throws<InvalidDataException>(()=>UnifiedX64BackupSnapshot.Create(plan,staged));
        Assert.Equal(receipt,UnifiedX64BackupSnapshot.Verify(receipt.Directory));
    });

    [Fact]
    public void ChangedStagedZipBlocksBackup()=>Run((plan,staged)=>{
        File.WriteAllBytes(staged.ZipPath,new byte[]{5,2,3,4});
        Assert.Throws<InvalidDataException>(()=>UnifiedX64BackupSnapshot.Create(plan,staged));
        Assert.False(Directory.Exists(Path.GetDirectoryName(plan.BackupDirectory)!));
    });
}
