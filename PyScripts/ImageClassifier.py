import os
import json
import datetime
import argparse
import torch
from torch.optim.optimizer import required
from torchvision import transforms
from torchvision import models
from PIL import Image

# 可用的模型名称（不再从 torchvision 中直接加载）
MODEL_NAMES = {
    'densenet121': models.densenet121,
    'densenet161': models.densenet161,
    'densenet169': models.densenet169,
    'densenet201': models.densenet201,
    'efficientnet_b0': models.efficientnet_b0,
    'efficientnet_b1': models.efficientnet_b1,
    'efficientnet_b2': models.efficientnet_b2,
    'efficientnet_b3': models.efficientnet_b3,
    'efficientnet_b4': models.efficientnet_b4,
    'efficientnet_b5': models.efficientnet_b5,
    'efficientnet_b6': models.efficientnet_b6,
    'efficientnet_b7': models.efficientnet_b7,
    'efficientnet_v2_l': models.efficientnet_v2_l,
    'efficientnet_v2_m': models.efficientnet_v2_m,
    'efficientnet_v2_s': models.efficientnet_v2_s,
    'mobilenet_v2': models.mobilenet_v2,
    'mobilenet_v3_large': models.mobilenet_v3_large,
    'mobilenet_v3_small': models.mobilenet_v3_small,
    'resnet101': models.resnet101,
    'resnet152': models.resnet152,
    'resnet18': models.resnet18,
    'resnet34': models.resnet34,
    'resnet50': models.resnet50,
    'vgg11': models.vgg11,
    'vgg11_bn': models.vgg11_bn,
    'vgg13': models.vgg13,
    'vgg13_bn': models.vgg13_bn,
    'vgg16': models.vgg16,
    'vgg16_bn': models.vgg16_bn,
    'vgg19': models.vgg19,
    'vgg19_bn': models.vgg19_bn,
}

# 图像预处理
preprocess = transforms.Compose([
    transforms.Resize(256),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
])

# 定义图像分类预测函数
def predict_images(model, image_folder, output_json_file):
    # model.eval()
    results = []

    for image_name in os.listdir(image_folder):
        image_path = os.path.join(image_folder, image_name)
        image = Image.open(image_path).convert('RGB')
        tensor = preprocess(image).unsqueeze(0)  # 添加批次维度

        with torch.no_grad():
            output = model(tensor)
            confidence, predicted_class = torch.max(torch.nn.functional.softmax(output[0], dim=0), dim=0)

        results.append({
            'image_path': image_path,
            'predicted_class': predicted_class.item(),
            'confidence': confidence.item()
        })

    # 存储结果为 JSON 文件
    with open(output_json_file, 'w') as f:
        json.dump(results, f, indent=4)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--image-folder', required=True, help='Path to the directory with images to predict')
    parser.add_argument('--model-path', required=True, help='Path to the pre-trained model weights file')
    parser.add_argument('--model-name',required=True,help='')
    parser.add_argument('--num-classes',type=int, required=True,help='')
    parser.add_argument('--output-path', required=True, help='Path to save the output JSON file')
    args = parser.parse_args()

    # 加载模型
    model_path = args.model_path
    model = MODEL_NAMES[args.model_name](weights=None, num_classes=args.num_classes)  # 获取模型构造函数
    # 加载权重
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model.load_state_dict(torch.load(model_path,map_location=device, weights_only=True))
    model.eval()  # 设置为评估模式

    # 调用预测函数
    predict_images(model, args.image_folder, args.output_path)
    print(f"Predictions saved to {args.output_path}")

if __name__ == '__main__':
    main()