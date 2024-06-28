# Note: some modules are lazily-imported within functions below
import os
import json
import sys

def process_images(path, language):
    model_hashes = None
    reader = None

    for root, _, files in os.walk(path):
        for filename in files:
            if filename.lower().endswith(('.png', '.jpg')):
                image_path = os.path.join(root, filename)

                # Create a 'text' subdirectory if it doesn't exist
                text_subdir = os.path.join(root, 'text')
                os.makedirs(text_subdir, exist_ok=True)

                # Create a JSON file path within the 'text' subdirectory
                json_filename = os.path.splitext(filename)[0] + '.easyocr.json'
                json_path = os.path.join(text_subdir, json_filename)

                if not os.path.exists(json_path):
                    if not model_hashes:
                        model_hashes = get_model_hashes()
                    if not reader:
                        reader = get_easyocr_reader(language)

                    # Perform OCR and get the list of JSON strings
                    json_strings = reader.readtext(image_path, output_format='json')

                    # Deserialize each JSON string to a Python object
                    results = [json.loads(json_str) for json_str in json_strings]

                    # Create the wrapper object
                    wrapper = {
                        "models": model_hashes,
                        "readResult": results
                    }

                    with open(json_path, 'w') as json_file:
                        json.dump(wrapper, json_file, ensure_ascii=False, indent=None)
                    print(f"{json_path}")
                    print(f"Processed {filename} and saved results to text/{json_filename}", file=sys.stderr)

def get_model_hashes():
    # Compute MD5 hashes for all .pth files in ~/.EasyOCR/model.
    models_dir = os.path.expanduser('~/.EasyOCR/model')

    model_hashes_cache = os.path.join(models_dir, 'model_hashes.json')
    if os.path.exists(model_hashes_cache):
        with open(model_hashes_cache, 'r') as cache_file:
            model_hashes = json.load(cache_file)
        return model_hashes

    model_hashes = {}
    for model_file in os.listdir(models_dir):
        if model_file.endswith('.pth'):
            model_path = os.path.join(models_dir, model_file)
            model_hashes[model_file] = compute_md5(model_path)

    with open(model_hashes_cache, 'w') as cache_file:
        json.dump(model_hashes, cache_file, ensure_ascii=False, indent=None)

    return model_hashes

def compute_md5(file_path):
    import hashlib
    # Compute the MD5 hash of a file.
    hasher = hashlib.md5()
    with open(file_path, 'rb') as f:
        while chunk := f.read(8192):
            hasher.update(chunk)
    return hasher.hexdigest()

def get_easyocr_reader(language):
    from easyocr import Reader
    return Reader([language])

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python TandokuImagesAnalyze_EasyOcr.py /path/to/your/images language")
        sys.exit(1)

    path_to_images = sys.argv[1]
    language_to_use = sys.argv[2]
    process_images(path_to_images, language_to_use)
