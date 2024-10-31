
import sys
import json
import torch.nn as nn
from torchvision import datasets
import torchvision.transforms as transforms
import torch.optim as optim
from tqdm import tqdm
import matplotlib.pyplot as plt
import torchvision.models as models
import argparse
import random
import numpy as np
import torch
import os
import sklearn
import pandas as pd

# 设置随机种子
seed = 2024
random.seed(seed)
np.random.seed(seed)
sklearn.utils.check_random_state(seed)

# PyTorch 设置随机种子
torch.manual_seed(seed)
torch.cuda.manual_seed(seed)
torch.cuda.manual_seed_all(seed)  # 如果使用多张GPU
torch.backends.cudnn.deterministic = True
torch.backends.cudnn.benchmark = False

# 固定 Pandas 的随机性
pd.set_option('mode.chained_assignment', None)
pd.set_option('compute.use_bottleneck', False)
pd.set_option('compute.use_numexpr', False)


class EarlyStopping:
    """Early stops the training if validation loss doesn't improve after a given patience."""
    def __init__(self, save_path, patience=5, verbose=True, delta=0):

        self.save_path = save_path
        self.patience = patience
        self.verbose = verbose
        self.counter = 0
        self.best_score = None
        self.early_stop = False
        self.val_loss_min = np.Inf
        self.delta = delta

    def __call__(self, val_loss, model):

        score = -val_loss

        if self.best_score is None:
            self.best_score = score
            self.save_checkpoint(val_loss, model)
        elif score < self.best_score + self.delta:
            self.counter += 1
            print(f'EarlyStopping counter: {self.counter} out of {self.patience}')
            if self.counter >= self.patience:
                self.early_stop = True
        else:
            self.best_score = score
            self.save_checkpoint(val_loss, model)
            self.counter = 0

    def save_checkpoint(self, val_loss, model):
        '''Saves model when validation loss decrease.'''
        if self.verbose:
            print(f'Validation loss decreased ({self.val_loss_min:.6f} --> {val_loss:.6f}).  Saving model ...')
        # path = os.path.join(self.save_path, 'best_network.pth')
        torch.save(model.state_dict(), self.save_path)	# 这里会存储迄今最优模型的参数
        self.val_loss_min = val_loss

def train(data_path, class_name, pretrain_path, model_path):

    # 启用早停
    early_stopping = EarlyStopping(model_path)

    # 指定使用CPU
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"use device is {device}")


    data_transform = {
        # 训练集图像预处理步骤
        "train": transforms.Compose([
                                    transforms.Resize(224), # 模型要求输入为224 * 224
                                    transforms.RandomHorizontalFlip(),  # 水平随机翻转
                                    transforms.RandomVerticalFlip(),    # 垂直随机翻转
                                    transforms.ToTensor(), # 转换为张量
                                    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]), # 这里需要与原始大模型预训练的处理方式保持统一，无需修改
        ]),
        # 验证集图像预处理步骤
        "val": transforms.Compose([
                                    transforms.Resize(224),
                                    transforms.ToTensor(),
                                    transforms.Normalize([0.485, 0.456, 0.406], [0.229, 0.224, 0.225]),
        ])
    }

    image_path = data_path
    assert os.path.exists(image_path), "{} path does not exist.".format(image_path)

    batch_size = 8 # 超参数1：批大小，根据训练集大小调整
    nw = 0
    print(f"Using {nw} dataloader workers every process.")

    # 加载数据集
    train_dataset = datasets.ImageFolder(root=os.path.join(image_path, "train"),
                                            transform=data_transform["train"]
                                            )
    train_num = len(train_dataset)
    train_loader = torch.utils.data.DataLoader(train_dataset,
                                                batch_size=batch_size,
                                                shuffle=True,
                                                num_workers=nw
                                                ) # shuffle打乱数据集，提高模型健壮性

    val_dataset = datasets.ImageFolder(root=os.path.join(image_path, "val"),
                                        transform=data_transform["val"]
                                        )
    val_num = len(val_dataset)
    val_loader = torch.utils.data.DataLoader(val_dataset,
                                                batch_size=4,
                                                shuffle=False,
                                                num_workers=nw
                                                ) # 验证时无需打乱


    class_list = train_dataset.class_to_idx
    cla_dict = dict((val, key) for key, val in class_list.items())
    json_str = json.dumps(cla_dict, indent=4)
    with open(class_name, 'w') as json_file:
        json_file.write(json_str)

    print(f"Using {train_num} images for training, {val_num} images for validation.")

    # # 实例化ResNet-18模型
    net = models.resnet18(weights=None)
    model_weight_path = pretrain_path
    assert os.path.exists(model_weight_path), "file {} does not exist.".format(model_weight_path)
    net.load_state_dict(torch.load(model_weight_path, map_location='cpu'))

    # 修改全连接层结构
    # 这里更改全连接层,将原始模型的1000分类修改为我们需要的5分类
    in_channel = net.fc.in_features
    net.fc = nn.Linear(in_channel, 5)   # (5分类)

    net.to(device)  # 将模型移动到GPU
    loss_function = nn.CrossEntropyLoss()   # 使用交叉熵损失函数
    # 构建一个优化器
    params = [p for p in net.parameters() if p.requires_grad]
    optimizer = optim.Adam(params, lr=0.0001)     # 优化器(训练参数, 学习率)

    # 2024/8/6 启用早停，epochs应尽可能大
    epochs = 2000     # 训练轮数,需要根据训练集大小来修改

    train_losses = []  # 存储训练损失的列表
    val_losses = []    # 存储验证损失的列表

    train_acces = []  # 存储训练准确性的列表
    val_acces = []    # 存储验证准确性的列表

    true_epoch = 0

    for epoch in range(epochs):
        # 训练阶段
        net.train()  # 启用dropout, batch norm等
        running_loss = 0.0
        running_acc = 0.0
        train_bar = tqdm(train_loader, file=sys.stdout)  # 训练进度条

        for step, data in enumerate(train_bar):  # 遍历每个批次
            images, labels = data  # 获取图像和标签
            images, labels = images.to(device), labels.to(device)
            optimizer.zero_grad()  # 清除先前的梯度
            logits = net(images)  # 前向传播
            loss = loss_function(logits, labels)  # 计算损失
            loss.backward()  # 反向传播
            optimizer.step()  # 更新优化器参数

            running_loss += loss.item()  # 累积损失

            train_bar.desc = "train epoch [{}/{}] loss:{:.3f}".format(epoch + 1, epochs, loss.item())

            out = torch.max(logits, dim=1)[1]  # 预测标签
            running_acc += torch.eq(out, labels).sum().item()

        train_loss = running_loss / len(train_loader)  # 每个epoch的平均训练损失
        train_losses.append(train_loss)  # 记录训练损失

        train_accuracy = running_acc / len(train_loader.dataset)  # 计算训练准确率
        train_acces.append(train_accuracy)

        # 验证阶段
        net.eval()  # 禁用dropout, batch norm等
        val_loss = 0.0
        acc = 0.0

        with torch.no_grad():  # 验证阶段不需要梯度

            val_bar = tqdm(val_loader, file=sys.stdout)  # 验证进度条

            for val_data in val_bar:

                val_images, val_labels = val_data
                val_images, val_labels = val_images.to(device), val_labels.to(device)
                outputs = net(val_images)  # 前向传播


                loss = loss_function(outputs, val_labels)  # 计算验证损失
                val_loss += loss.item()  # 累积验证损失
                predict_y = torch.max(outputs, dim=1)[1]  # 预测标签

                acc += torch.eq(predict_y, val_labels).sum().item()  # 计算正确预测数
        val_loss /= len(val_loader)  # 每个epoch的平均验证损失
        val_losses.append(val_loss)  # 记录验证损失

        val_accuracy = acc / len(val_loader.dataset)  # 计算验证准确率
        val_acces.append(val_accuracy)
        print("[epoch %d ] train_loss: %.3f  val_loss: %.3f  train_acc: %.3f  val_acc: %.3f" % (epoch + 1, train_loss, val_loss, train_accuracy, val_accuracy))

        early_stopping(val_loss, net)
        #达到早停止条件时，early_stop会被置为True
        if early_stopping.early_stop:
            print("Early stopping")
            true_epoch = epoch
            break #跳出迭代，结束训练


    print("训练完成.")

    # 绘制训练和验证损失图
    plt.figure(figsize=(10, 5))
    plt.plot(range(1, true_epoch + 2), train_losses, label='Training Loss')
    plt.plot(range(1, true_epoch + 2), val_losses, label='Validation Loss')
    plt.plot(range(1, true_epoch + 2), train_acces, label='Training Acc')
    plt.plot(range(1, true_epoch + 2), val_acces, label='Validation Acc')
    plt.xlabel('Epochs')
    plt.ylabel('Loss')
    plt.title('Training and Validation Loss')
    plt.legend()
    plt.grid(True)
    plt.savefig('./output/loss_resnet_18.png')
    plt.show()

    def evaluate(model, val_loader, loss_function, device):
        model.eval()  # 设置模型为评估模式
        val_loss = 0.0
        correct = 0
        total = 0
        with torch.no_grad():  # 禁用梯度计算
            for val_data in val_loader:
                val_images, val_labels = val_data
                val_images, val_labels = val_images.to(device), val_labels.to(device)

                outputs = model(val_images)  # 前向传播
                loss = loss_function(outputs, val_labels)  # 计算损失
                val_loss += loss.item()  # 累积损失

                # 预测标签
                _, predicted = torch.max(outputs, 1)
                total += val_labels.size(0)  # 总样本数
                correct += (predicted == val_labels).sum().item()  # 正确预测的样本数

        val_loss /= len(val_loader)  # 计算平均损失
        val_accuracy = correct / total  # 计算准确率
        return val_loss, val_accuracy


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    
    parser.add_argument('--traindata', type=str, default = "../_data")
    parser.add_argument('--classname', type=str, default = "../class_indices.json")
    parser.add_argument('--pretrain', type=str, default = "../resnet18-5c106cde.pth")
    parser.add_argument('--modelpath', type=str, default = "../ResNet18.pth")

    args = parser.parse_args()
    train(args.traindata, args.classname, args.pretrain, args.modelpath)
