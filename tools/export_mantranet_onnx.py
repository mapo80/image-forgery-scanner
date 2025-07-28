import os
import requests
import keras2onnx
from keras.models import load_model
import keras
import tensorflow as tf

if keras.__version__ != "2.2.0" or tf.__version__ != "1.8.0":
    raise RuntimeError(
        "Requires Keras 2.2.0 and TensorFlow 1.8.0, got "
        f"Keras {keras.__version__} / TensorFlow {tf.__version__}")

H5_URL = "https://github.com/ISICV/ManTraNet/raw/master/pretrained_weights/mantranet.h5"
H5_PATH = "ManTraNet/pretrained_weights/mantranet.h5"

os.makedirs(os.path.dirname(H5_PATH), exist_ok=True)
if not os.path.isfile(H5_PATH):
    print(f"Downloading weights from {H5_URL}")
    r = requests.get(H5_URL, stream=True)
    r.raise_for_status()
    with open(H5_PATH, "wb") as f:
        for chunk in r.iter_content(chunk_size=8192):
            f.write(chunk)
    print("Downloaded pretrained weights to", H5_PATH)

model = load_model(H5_PATH)

onnx_model = keras2onnx.convert_keras(model, model.name, target_opset=12)

OUT_PATH = "src/Models/onnx/mantranet_256x256.onnx"
os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
keras2onnx.save_model(onnx_model, OUT_PATH)
print("Saved ONNX model to", OUT_PATH)
