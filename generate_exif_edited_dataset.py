import os
from PIL import Image
import piexif

# === CARTELLE ===
ORIGINAL_DIR = "dataset/exif/original"
EDITED_DIR = "dataset/exif/exif_edited"
os.makedirs(EDITED_DIR, exist_ok=True)

# === FUNZIONE PER MODIFICARE I METADATI EXIF ===
def edit_exif(input_path, output_path):
    img = Image.open(input_path)
    exif_dict = {"0th": {}, "Exif": {}, "GPS": {}, "1st": {}, "thumbnail": None}

    exif_dict["0th"][piexif.ImageIFD.Make] = b"FakeBrand"
    exif_dict["0th"][piexif.ImageIFD.Model] = b"ForgeryCam 3000"
    exif_dict["Exif"][piexif.ExifIFD.DateTimeOriginal] = b"2020:01:01 12:00:00"

    # Coordinate GPS false (es: Roma)
    exif_dict["GPS"][piexif.GPSIFD.GPSLatitudeRef] = b'N'
    exif_dict["GPS"][piexif.GPSIFD.GPSLatitude] = [(41, 1), (53, 1), (0, 1)]
    exif_dict["GPS"][piexif.GPSIFD.GPSLongitudeRef] = b'E'
    exif_dict["GPS"][piexif.GPSIFD.GPSLongitude] = [(12, 1), (29, 1), (0, 1)]

    exif_bytes = piexif.dump(exif_dict)
    img.save(output_path, "JPEG", exif=exif_bytes)

# === PROCESSA TUTTE LE IMMAGINI ===
for filename in os.listdir(ORIGINAL_DIR):
    if filename.lower().endswith((".jpg", ".jpeg")):
        input_path = os.path.join(ORIGINAL_DIR, filename)
        output_path = os.path.join(EDITED_DIR, filename)
        try:
            edit_exif(input_path, output_path)
            print(f"✅ {filename} modificata.")
        except Exception as e:
            print(f"❌ Errore con {filename}: {e}")
