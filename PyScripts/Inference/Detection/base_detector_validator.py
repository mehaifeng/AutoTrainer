"""
检测任务基础验证器
提供检测模型验证的通用框架
"""
import torch
import torch.nn as nn
from PIL import Image, ImageDraw
from torchvision.transforms import functional as F
import json
import os
import sys
import time
import numpy as np
from pathlib import Path
from typing import Dict, List, Tuple, Any
from abc import ABC, abstractmethod
from pycocotools.coco import COCO


class BaseDetectionValidator(ABC):
    """检测验证器基类"""
    
    def __init__(self, config_path: str):
        """
        初始化验证器
        
        Args:
            config_path: 配置文件路径
        """
        with open(config_path, 'r') as f:
            self.config = json.load(f)
        
        self.device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        self.model = None
        self.coco = None
        self.categories = {}
        
        print(f"使用设备: {self.device}", flush=True)
    
    @abstractmethod
    def load_model(self, model_path: str, num_classes: int) -> nn.Module:
        """
        加载模型
        
        Args:
            model_path: 模型权重路径
            num_classes: 类别数（包含背景）
            
        Returns:
            加载好的模型
        """
        pass
    
    @abstractmethod
    def get_model_name(self) -> str:
        """获取模型名称"""
        pass
    
    def _get_num_classes_from_model(self, model_path: str) -> Tuple[int, Dict[int, str]]:
        """
        从模型checkpoint获取类别数和类别映射
        
        Args:
            model_path: 模型文件路径
            
        Returns:
            (num_classes, categories_dict) - 类别数和类别映射字典
        """
        try:
            checkpoint = torch.load(model_path, map_location='cpu')
            
            if isinstance(checkpoint, dict):
                # 优先从metadata读取
                if 'num_classes' in checkpoint:
                    num_classes = checkpoint['num_classes']
                    print(f"从metadata读取类别数: {num_classes}", flush=True)
                    
                    # 尝试读取类别名称（如果有）
                    categories = checkpoint.get('categories', {})
                    return num_classes, categories
                
                # 回退：从权重shape推断
                if 'model_state_dict' in checkpoint:
                    state_dict = checkpoint['model_state_dict']
                elif 'state_dict' in checkpoint:
                    state_dict = checkpoint['state_dict']
                else:
                    state_dict = checkpoint
                
                # 从分类头推断类别数
                for key in ['roi_heads.box_predictor.cls_score.weight',
                           'head.classification_head.cls_logits.weight']:
                    if key in state_dict:
                        num_classes = state_dict[key].shape[0]
                        print(f"从权重shape推断类别数: {num_classes}", flush=True)
                        return num_classes, {}
            
            raise ValueError("无法从模型文件推断类别数")
            
        except Exception as e:
            raise RuntimeError(f"读取模型metadata失败: {e}")
    
    def load_coco_annotations(self, coco_path: str):
        """
        加载COCO格式标注
        
        Args:
            coco_path: COCO标注文件路径
        """
        print(f"加载COCO标注: {coco_path}", flush=True)
        self.coco = COCO(coco_path)
        
        # 构建类别映射
        self.categories = {cat['id']: cat['name'] 
                          for cat in self.coco.loadCats(self.coco.getCatIds())}
        
        print(f"找到 {len(self.coco.imgs)} 张图片", flush=True)
        print(f"找到 {len(self.categories)} 个类别", flush=True)
    
    def get_ground_truth_boxes(self, img_id: int) -> List[Dict]:
        """
        获取真实标注框
        
        Args:
            img_id: 图片ID
            
        Returns:
            标注框列表
        """
        gt_boxes = []
        try:
            ann_ids = self.coco.getAnnIds(imgIds=img_id)
            anns = self.coco.loadAnns(ann_ids)
            
            for ann in anns:
                bbox = ann['bbox']  # COCO格式: [x, y, width, height]
                x1, y1, w, h = bbox
                x2, y2 = x1 + w, y1 + h
                
                cat_id = ann['category_id']
                category = self.coco.loadCats(cat_id)[0]['name']
                
                gt_boxes.append({
                    "coords": [int(x1), int(y1), int(x2), int(y2)],
                    "label": category
                })
        except Exception as e:
            print(f"警告: 加载真实框失败: {e}", file=sys.stderr, flush=True)
        
        return gt_boxes
    
    def run_inference(self, image_path: str, confidence_threshold: float = 0.5) -> List[Dict]:
        """
        对单张图片进行推理
        
        Args:
            image_path: 图片路径
            confidence_threshold: 置信度阈值
            
        Returns:
            预测框列表
        """
        try:
            # 加载并预处理图片
            img = Image.open(image_path).convert('RGB')
            img_tensor = F.to_tensor(img).unsqueeze(0).to(self.device)
            
            # 推理
            with torch.no_grad():
                predictions = self.model(img_tensor)
            
            pred_boxes = []
            if len(predictions) > 0:
                pred = predictions[0]
                
                # 按置信度过滤
                scores = pred['scores'].cpu().numpy()
                boxes = pred['boxes'].cpu().numpy()
                labels = pred['labels'].cpu().numpy()
                
                for i, score in enumerate(scores):
                    if score >= confidence_threshold:
                        box = boxes[i]
                        label_id = int(labels[i])
                        
                        # 映射标签ID到类别名
                        label_name = self.categories.get(label_id, f"class_{label_id}")
                        
                        pred_boxes.append({
                            "coords": [int(box[0]), int(box[1]), int(box[2]), int(box[3])],
                            "label": label_name,
                            "confidence": float(score)
                        })
            
            return pred_boxes
        except Exception as e:
            print(f"推理错误 {image_path}: {e}", file=sys.stderr, flush=True)
            import traceback
            traceback.print_exc()
            return []
    
    def draw_boxes_on_image(self, image_path: str, gt_boxes: List[Dict], 
                           pred_boxes: List[Dict], output_path: str) -> bool:
        """
        在图片上绘制标注框和预测框
        
        Args:
            image_path: 原始图片路径
            gt_boxes: 真实标注框
            pred_boxes: 预测框
            output_path: 输出路径
            
        Returns:
            是否成功
        """
        try:
            img = Image.open(image_path).convert('RGB')
            draw = ImageDraw.Draw(img)
            
            # 绘制真实框（绿色）
            for box in gt_boxes:
                coords = box['coords']
                draw.rectangle(coords, outline='green', width=3)
                try:
                    draw.text((coords[0], coords[1] - 10), box['label'], fill='green')
                except:
                    pass
            
            # 绘制预测框（红色）
            for box in pred_boxes:
                coords = box['coords']
                draw.rectangle(coords, outline='red', width=2)
                try:
                    label_text = f"{box['label']} {box['confidence']:.2f}"
                    draw.text((coords[0], coords[1] - 10), label_text, fill='red')
                except:
                    pass
            
            img.save(output_path)
            return True
        except Exception as e:
            print(f"绘制框失败 {image_path}: {e}", file=sys.stderr, flush=True)
            return False
    
    def calculate_iou(self, box1: List[int], box2: List[int]) -> float:
        """
        计算两个框的IoU
        
        Args:
            box1, box2: 格式为 [x1, y1, x2, y2]
            
        Returns:
            IoU值
        """
        x1_min, y1_min, x1_max, y1_max = box1
        x2_min, y2_min, x2_max, y2_max = box2
        
        # 交集
        xi_min = max(x1_min, x2_min)
        yi_min = max(y1_min, y2_min)
        xi_max = min(x1_max, x2_max)
        yi_max = min(y1_max, y2_max)
        
        if xi_max <= xi_min or yi_max <= yi_min:
            return 0.0
        
        intersection = (xi_max - xi_min) * (yi_max - yi_min)
        
        # 并集
        area1 = (x1_max - x1_min) * (y1_max - y1_min)
        area2 = (x2_max - x2_min) * (y2_max - y2_min)
        union = area1 + area2 - intersection
        
        return intersection / union if union > 0 else 0.0
    
    def calculate_metrics(self, all_gt_boxes: List[List[Dict]], 
                         all_pred_boxes: List[List[Dict]], 
                         iou_threshold: float = 0.5) -> Tuple[float, float]:
        """
        计算检测指标
        
        Args:
            all_gt_boxes: 所有图片的真实框
            all_pred_boxes: 所有图片的预测框
            iou_threshold: IoU阈值
            
        Returns:
            (precision, recall)
        """
        true_positives = 0
        false_positives = 0
        false_negatives = 0
        
        for gt_boxes, pred_boxes in zip(all_gt_boxes, all_pred_boxes):
            matched_gt = set()
            
            for pred_box in pred_boxes:
                best_iou = 0
                best_gt_idx = -1
                
                for idx, gt_box in enumerate(gt_boxes):
                    if idx in matched_gt:
                        continue
                    iou = self.calculate_iou(pred_box['coords'], gt_box['coords'])
                    if iou > best_iou:
                        best_iou = iou
                        best_gt_idx = idx
                
                if best_iou >= iou_threshold:
                    true_positives += 1
                    matched_gt.add(best_gt_idx)
                else:
                    false_positives += 1
            
            false_negatives += len(gt_boxes) - len(matched_gt)
        
        precision = true_positives / (true_positives + false_positives) if (true_positives + false_positives) > 0 else 0
        recall = true_positives / (true_positives + false_negatives) if (true_positives + false_negatives) > 0 else 0
        
        return precision, recall
    
    def validate(self) -> Dict[str, Any]:
        """
        执行验证
        
        Returns:
            验证结果字典
        """
        start_time = time.time()
        
        model_path = self.config['model_weights_path']
        coco_path = self.config.get('coco_annotation_path')
        
        # 首先从模型metadata获取num_classes和类别信息
        num_classes, categories_from_metadata = self._get_num_classes_from_model(model_path)
        
        # 判断是否有COCO标注
        has_coco = coco_path and os.path.exists(coco_path)
        
        if has_coco:
            print(f"检测到COCO标注文件，将绘制真实框和预测框", flush=True)
            self.load_coco_annotations(coco_path)
            
            # 验证类别数是否一致
            coco_num_classes = len(self.categories) + 1
            if num_classes != coco_num_classes:
                print(f"警告: 模型类别数({num_classes})与COCO标注类别数({coco_num_classes})不一致", 
                      flush=True)
                print(f"将使用模型的类别数: {num_classes}", flush=True)
        else:
            print(f"未提供COCO标注文件，仅绘制预测框", flush=True)
            # 使用从metadata读取的类别信息
            self.categories = categories_from_metadata or {}
        
        # 加载模型
        print(f"加载模型: {model_path}", flush=True)
        print(f"类别数（含背景）: {num_classes}", flush=True)
        
        self.model = self.load_model(model_path, num_classes)
        self.model.to(self.device)
        self.model.eval()
        
        # 创建输出目录
        config_dir = Path(self.config['model_weights_path']).parent.parent / 'Configs'
        output_dir = config_dir / 'detection_output'
        output_dir.mkdir(parents=True, exist_ok=True)
        print(f"输出目录: {output_dir}", flush=True)
        
        # 推理
        validation_path = self.config['validation_data_path']
        confidence_threshold = self.config.get('confidence_threshold', 0.5)
        
        print(f"置信度阈值: {confidence_threshold}", flush=True)
        print("开始推理...", flush=True)
        
        image_results = []
        all_gt_boxes = []
        all_pred_boxes = []
        
        # 获取图片列表
        if has_coco:
            # 从COCO标注获取图片列表
            img_ids = self.coco.getImgIds()
            image_files = []
            for img_id in img_ids:
                img_info = self.coco.loadImgs(img_id)[0]
                image_files.append((img_id, img_info['file_name']))
        else:
            # 从目录直接扫描图片
            image_extensions = {'.jpg', '.jpeg', '.png', '.bmp', '.webp'}
            image_files = []
            for file in os.listdir(validation_path):
                if Path(file).suffix.lower() in image_extensions:
                    image_files.append((None, file))
        
        total_images = len(image_files)
        
        for idx, (img_id, img_filename) in enumerate(image_files):
            img_path = os.path.join(validation_path, img_filename)
            
            if not os.path.exists(img_path):
                print(f"警告: 图片不存在: {img_path}", file=sys.stderr, flush=True)
                continue
            
            print(f"处理 {idx+1}/{total_images}: {img_filename}", flush=True)
            
            # 获取真实框（如果有COCO标注）
            if has_coco and img_id is not None:
                gt_boxes = self.get_ground_truth_boxes(img_id)
            else:
                gt_boxes = []
            
            # 运行推理
            pred_boxes = self.run_inference(img_path, confidence_threshold)
            
            all_gt_boxes.append(gt_boxes)
            all_pred_boxes.append(pred_boxes)
            
            # 绘制并保存
            output_filename = f"annotated_{img_filename}"
            output_path = output_dir / output_filename
            
            if self.draw_boxes_on_image(img_path, gt_boxes, pred_boxes, str(output_path)):
                image_results.append({
                    "path": str(output_path),
                    "ground_truth_boxes": gt_boxes,
                    "predicted_boxes": pred_boxes
                })
            else:
                image_results.append({
                    "path": img_path,
                    "ground_truth_boxes": gt_boxes,
                    "predicted_boxes": pred_boxes
                })
        
        # 计算指标（仅当有真实框时）
        if has_coco:
            print("计算指标...", flush=True)
            precision_50, recall_50 = self.calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=0.5)
            precision_75, recall_75 = self.calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=0.75)
            
            # 计算mAP（简化版）
            map_scores = []
            for iou_thresh in np.arange(0.5, 1.0, 0.05):
                p, r = self.calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=iou_thresh)
                map_scores.append(p)
            
            map_50_95 = np.mean(map_scores) if map_scores else 0
            
            # 生成PR曲线点
            pr_points = []
            confidence_thresholds = np.arange(0.1, 1.0, 0.05)
            for conf_thresh in confidence_thresholds:
                # 按置信度过滤预测
                filtered_preds = []
                for pred_boxes in all_pred_boxes:
                    filtered = [box for box in pred_boxes if box['confidence'] >= conf_thresh]
                    filtered_preds.append(filtered)
                
                p, r = self.calculate_metrics(all_gt_boxes, filtered_preds, iou_threshold=0.5)
                pr_points.append({"x": round(r, 3), "y": round(p, 3)})
            
            pr_points = sorted(pr_points, key=lambda x: x['x'])
        else:
            # 无COCO标注时，指标为0
            print("无COCO标注，跳过指标计算", flush=True)
            precision_50 = 0.0
            recall_50 = 0.0
            map_50_95 = 0.0
            pr_points = []
        
        elapsed_time = time.time() - start_time
        
        # 构建结果
        results = {
            'metrics': {
                'time': round(elapsed_time, 2),
                'map50': precision_50,
                'map50_95': map_50_95,
                'precision': precision_50,
                'recall_macro': recall_50
            },
            'chart_data': {
                'pr_curve_points': pr_points,
                'labels': list(self.categories.values()) if self.categories else []
            },
            'image_results': image_results
        }
        
        if has_coco:
            print(f"\n验证完成!", flush=True)
            print(f"Precision@0.5: {precision_50:.3f}", flush=True)
            print(f"Recall@0.5: {recall_50:.3f}", flush=True)
            print(f"mAP@0.5:0.95: {map_50_95:.3f}", flush=True)
        else:
            print(f"\n推理完成!", flush=True)
            print(f"处理图片数: {total_images}", flush=True)
        print(f"mAP@0.5:0.95: {map_50_95:.3f}", flush=True)
        print(f"耗时: {elapsed_time:.2f}秒", flush=True)
        
        return results
    
    def run(self):
        """运行验证并保存结果"""
        try:
            results = self.validate()
            
            # 保存结果
            config_dir = Path(self.config['model_weights_path']).parent.parent / 'Configs'
            output_path = config_dir / 'detection_results.json'
            
            with open(output_path, 'w') as f:
                json.dump(results, f, indent=4)
            
            print(f"\n结果已保存至: {output_path}", flush=True)
            
        except Exception as e:
            print(f"验证过程出错: {e}", file=sys.stderr, flush=True)
            import traceback
            traceback.print_exc()
            sys.exit(1)
