namespace ImageForensics.Core.Models;

[System.Flags]
public enum ForensicsCheck
{
    None        = 0,
    Ela         = 1 << 0,
    CopyMove    = 1 << 1,
    Splicing    = 1 << 2,
    Inpainting  = 1 << 3,
    Exif        = 1 << 4,
    All = Ela | CopyMove | Splicing | Inpainting | Exif
}
