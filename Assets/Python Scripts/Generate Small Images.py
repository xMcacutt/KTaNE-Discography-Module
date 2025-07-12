import os
from PIL import Image
input_folder = './'
output_folder = './small'
os.makedirs(output_folder, exist_ok=True)
for filename in os.listdir(input_folder):
    if filename.lower().endswith('.png'):
        input_path = os.path.join(input_folder, filename)
        output_path = os.path.join(output_folder, filename)
        with Image.open(input_path) as img:
            resized_img = img.resize((10, 10), resample=Image.NEAREST)
            resized_img.save(output_path, format='PNG')
        print(f"Completed {output_path}")