import json
import time
import argparse
import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import torch
import torchvision
from torchvision.transforms import functional as F
from pycocotools.coco import COCO
from collections import defaultdict

def load_model(model_path, device, num_classes=None):
    """Load the trained detection model."""
    try:
        print(f"Loading model from {model_path}...", flush=True)
        
        # Try to load the checkpoint
        checkpoint = torch.load(model_path, map_location=device)
        
        # Check if it's a state_dict or a full model
        if isinstance(checkpoint, dict):
            # It's a state_dict or checkpoint dictionary
            if 'model_state_dict' in checkpoint:
                state_dict = checkpoint['model_state_dict']
            elif 'state_dict' in checkpoint:
                state_dict = checkpoint['state_dict']
            else:
                # Assume the whole dict is the state_dict
                state_dict = checkpoint
            
            # Need to create the model architecture
            # Try to infer model type from the path or use a default
            print("Detected state_dict format. Creating model architecture...", flush=True)
            
            # Infer number of classes from state_dict if not provided
            if num_classes is None:
                # Try to find num_classes from the classifier head
                for key in state_dict.keys():
                    if 'roi_heads.box_predictor.cls_score.weight' in key:
                        num_classes = state_dict[key].shape[0]
                        break
                    elif 'head.classification_head.cls_logits.weight' in key:
                        num_classes = state_dict[key].shape[0]
                        break
                
                if num_classes is None:
                    num_classes = 91  # COCO default
                    print(f"Warning: Could not infer num_classes, using default: {num_classes}", flush=True)
                else:
                    print(f"Inferred num_classes from state_dict: {num_classes}", flush=True)
            
            # Common detection models
            if 'fasterrcnn' in model_path.lower():
                if 'mobilenet' in model_path.lower():
                    from torchvision.models.detection import fasterrcnn_mobilenet_v3_large_fpn
                    model = fasterrcnn_mobilenet_v3_large_fpn(pretrained=False, num_classes=num_classes)
                else:
                    from torchvision.models.detection import fasterrcnn_resnet50_fpn
                    model = fasterrcnn_resnet50_fpn(pretrained=False, num_classes=num_classes)
            elif 'retinanet' in model_path.lower():
                from torchvision.models.detection import retinanet_resnet50_fpn
                model = retinanet_resnet50_fpn(pretrained=False, num_classes=num_classes)
            elif 'ssd' in model_path.lower():
                from torchvision.models.detection import ssd300_vgg16
                model = ssd300_vgg16(pretrained=False, num_classes=num_classes)
            else:
                # Default to Faster R-CNN
                print("Warning: Could not infer model type, using default Faster R-CNN ResNet50", flush=True)
                from torchvision.models.detection import fasterrcnn_resnet50_fpn
                model = fasterrcnn_resnet50_fpn(pretrained=False, num_classes=num_classes)
            
            # Load the state dict
            model.load_state_dict(state_dict, strict=False)
            print("Model state_dict loaded successfully.", flush=True)
        else:
            # It's already a model object
            model = checkpoint
            print("Full model object loaded.", flush=True)
        
        model.to(device)
        model.eval()
        return model
        
    except Exception as e:
        print(f"Error loading model: {e}", file=sys.stderr, flush=True)
        import traceback
        traceback.print_exc()
        sys.exit(1)

def get_ground_truth_boxes(coco, img_id, img_filename):
    """Get ground truth boxes from COCO annotation."""
    gt_boxes = []
    try:
        ann_ids = coco.getAnnIds(imgIds=img_id)
        anns = coco.loadAnns(ann_ids)
        
        for ann in anns:
            bbox = ann['bbox']  # COCO format: [x, y, width, height]
            x1, y1, w, h = bbox
            x2, y2 = x1 + w, y1 + h
            
            cat_id = ann['category_id']
            category = coco.loadCats(cat_id)[0]['name']
            
            gt_boxes.append({
                "coords": [int(x1), int(y1), int(x2), int(y2)],
                "label": category
            })
    except Exception as e:
        print(f"Warning: Could not load GT for {img_filename}: {e}", file=sys.stderr, flush=True)
    
    return gt_boxes

def run_inference(model, image_path, device, categories_map, confidence_threshold=0.5):
    """Run inference on a single image."""
    try:
        # Load and preprocess image
        img = Image.open(image_path).convert('RGB')
        img_tensor = F.to_tensor(img).unsqueeze(0).to(device)
        
        # Run inference
        with torch.no_grad():
            predictions = model(img_tensor)
        
        pred_boxes = []
        if len(predictions) > 0:
            pred = predictions[0]
            
            # Filter by confidence
            scores = pred['scores'].cpu().numpy()
            boxes = pred['boxes'].cpu().numpy()
            labels = pred['labels'].cpu().numpy()
            
            for i, score in enumerate(scores):
                if score >= confidence_threshold:
                    box = boxes[i]
                    label_id = int(labels[i])
                    
                    # Map label ID to category name
                    label_name = categories_map.get(label_id, f"class_{label_id}")
                    
                    pred_boxes.append({
                        "coords": [int(box[0]), int(box[1]), int(box[2]), int(box[3])],
                        "label": label_name,
                        "confidence": float(score)
                    })
        
        return pred_boxes
    except Exception as e:
        print(f"Error during inference on {image_path}: {e}", file=sys.stderr, flush=True)
        import traceback
        traceback.print_exc()
        return []

def draw_boxes_on_image(image_path, gt_boxes, pred_boxes, output_path):
    """Draw bounding boxes on image and save."""
    try:
        img = Image.open(image_path).convert('RGB')
        draw = ImageDraw.Draw(img)
        
        # Draw ground truth boxes in green
        for box in gt_boxes:
            coords = box['coords']
            draw.rectangle(coords, outline='green', width=3)
            # Optionally draw label
            try:
                draw.text((coords[0], coords[1] - 10), box['label'], fill='green')
            except:
                pass
        
        # Draw predicted boxes in red
        for box in pred_boxes:
            coords = box['coords']
            draw.rectangle(coords, outline='red', width=2)
            # Draw label with confidence
            try:
                label_text = f"{box['label']} {box['confidence']:.2f}"
                draw.text((coords[0], coords[1] - 10), label_text, fill='red')
            except:
                pass
        
        # Save the annotated image
        img.save(output_path)
        return True
    except Exception as e:
        print(f"Error drawing boxes on {image_path}: {e}", file=sys.stderr, flush=True)
        return False

def calculate_iou(box1, box2):
    """Calculate IoU between two boxes [x1, y1, x2, y2]."""
    x1_min, y1_min, x1_max, y1_max = box1
    x2_min, y2_min, x2_max, y2_max = box2
    
    # Intersection
    xi_min = max(x1_min, x2_min)
    yi_min = max(y1_min, y2_min)
    xi_max = min(x1_max, x2_max)
    yi_max = min(y1_max, y2_max)
    
    if xi_max <= xi_min or yi_max <= yi_min:
        return 0.0
    
    intersection = (xi_max - xi_min) * (yi_max - yi_min)
    
    # Union
    area1 = (x1_max - x1_min) * (y1_max - y1_min)
    area2 = (x2_max - x2_min) * (y2_max - y2_min)
    union = area1 + area2 - intersection
    
    return intersection / union if union > 0 else 0.0

def calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=0.5):
    """Calculate detection metrics."""
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
                iou = calculate_iou(pred_box['coords'], gt_box['coords'])
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

def main(config_path):
    """
    Real detection validation with model inference and COCO ground truth.
    """
    start_time = time.time()
    
    try:
        with open(config_path, 'r') as f:
            config = json.load(f)
        print("Configuration loaded successfully.", flush=True)
    except Exception as e:
        print(f"Error reading config file: {e}", file=sys.stderr, flush=True)
        sys.exit(1)

    print("Starting detection validation...", flush=True)

    # Get configuration
    validation_path = config.get("validation_data_path")
    model_weights_path = config.get("model_weights_path")
    coco_path = config.get("coco_annotation_path")
    confidence_threshold = config.get("confidence_threshold", 0.5)  # Default to 0.5 if not specified
    
    print(f"Confidence threshold: {confidence_threshold}", flush=True)
    
    # Validate inputs
    if not validation_path or not os.path.isdir(validation_path):
        print(f"Error: validation_data_path '{validation_path}' is invalid.", file=sys.stderr, flush=True)
        sys.exit(1)
    
    if not model_weights_path or not os.path.exists(model_weights_path):
        print(f"Error: model_weights_path '{model_weights_path}' is invalid.", file=sys.stderr, flush=True)
        sys.exit(1)
        
    if not coco_path or not os.path.exists(coco_path):
        print(f"Error: COCO annotation file '{coco_path}' is required and not found.", file=sys.stderr, flush=True)
        sys.exit(1)

    # Setup device
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    print(f"Using device: {device}", flush=True)
    
    # Load COCO annotations
    print(f"Loading COCO annotations from {coco_path}...", flush=True)
    try:
        coco = COCO(coco_path)
        print(f"COCO annotations loaded. Found {len(coco.imgs)} images.", flush=True)
    except Exception as e:
        print(f"Error loading COCO annotations: {e}", file=sys.stderr, flush=True)
        sys.exit(1)
    
    # Get number of classes from COCO
    num_classes = len(coco.getCatIds()) + 1  # +1 for background
    print(f"Number of classes (including background): {num_classes}", flush=True)
    
    # Load model
    model = load_model(model_weights_path, device, num_classes=num_classes)
    
    # Create output directory for annotated images
    output_dir = os.path.join(os.path.dirname(config_path), 'detection_output')
    os.makedirs(output_dir, exist_ok=True)
    print(f"Output directory: {output_dir}", flush=True)

    image_results = []
    all_gt_boxes = []
    all_pred_boxes = []
    
    # Get all category names for labels
    categories = {cat['id']: cat['name'] for cat in coco.loadCats(coco.getCatIds())}
    
    # Process images
    img_ids = coco.getImgIds()
    total_images = len(img_ids)
    
    for idx, img_id in enumerate(img_ids):
        img_info = coco.loadImgs(img_id)[0]
        img_filename = img_info['file_name']
        img_path = os.path.join(validation_path, img_filename)
        
        if not os.path.exists(img_path):
            print(f"Warning: Image not found: {img_path}", file=sys.stderr, flush=True)
            continue
        
        print(f"Processing {idx+1}/{total_images}: {img_filename}", flush=True)
        
        # Get ground truth boxes
        gt_boxes = get_ground_truth_boxes(coco, img_id, img_filename)
        
        # Run inference with category mapping and configured confidence threshold
        pred_boxes = run_inference(model, img_path, device, categories, confidence_threshold=confidence_threshold)
        
        all_gt_boxes.append(gt_boxes)
        all_pred_boxes.append(pred_boxes)
        
        # Draw boxes on image and save to output directory
        output_filename = f"annotated_{img_filename}"
        output_path = os.path.join(output_dir, output_filename)
        
        if draw_boxes_on_image(img_path, gt_boxes, pred_boxes, output_path):
            image_results.append({
                "path": output_path,
                "ground_truth_boxes": gt_boxes,
                "predicted_boxes": pred_boxes
            })
        else:
            # Fallback to original image if drawing fails
            image_results.append({
                "path": img_path,
                "ground_truth_boxes": gt_boxes,
                "predicted_boxes": pred_boxes
            })
    
    # Calculate metrics
    print("Calculating metrics...", flush=True)
    precision_50, recall_50 = calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=0.5)
    precision_75, recall_75 = calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=0.75)
    
    # Calculate mAP (simplified version - average over multiple IoU thresholds)
    map_scores = []
    for iou_thresh in np.arange(0.5, 1.0, 0.05):
        p, r = calculate_metrics(all_gt_boxes, all_pred_boxes, iou_threshold=iou_thresh)
        map_scores.append(p)
    
    map_50_95 = np.mean(map_scores) if map_scores else 0
    
    elapsed_time = time.time() - start_time
    
    metrics = {
        "time": round(elapsed_time, 2),
        "map50": precision_50,
        "map50_95": map_50_95,
        "precision": precision_50,
        "recall_macro": recall_50
    }
    
    # Generate PR curve points
    pr_points = []
    confidence_thresholds = np.arange(0.1, 1.0, 0.05)
    for conf_thresh in confidence_thresholds:
        # Filter predictions by confidence
        filtered_preds = []
        for pred_boxes in all_pred_boxes:
            filtered = [box for box in pred_boxes if box['confidence'] >= conf_thresh]
            filtered_preds.append(filtered)
        
        p, r = calculate_metrics(all_gt_boxes, filtered_preds, iou_threshold=0.5)
        pr_points.append({"x": round(r, 3), "y": round(p, 3)})
    
    # Sort by recall
    pr_points = sorted(pr_points, key=lambda x: x['x'])

    chart_data = {
        "pr_curve_points": pr_points,
        "labels": list(categories.values())
    }

    results = {
        "metrics": metrics,
        "chart_data": chart_data,
        "image_results": image_results
    }
    
    # Write results
    output_path = os.path.join(os.path.dirname(config_path), 'detection_results.json')
    try:
        with open(output_path, 'w') as f:
            json.dump(results, f, indent=4)
    except Exception as e:
        print(f"Error writing results file: {e}", file=sys.stderr, flush=True)
        sys.exit(1)

    print(f"\nValidation complete!", flush=True)
    print(f"  Precision@0.5: {precision_50:.3f}", flush=True)
    print(f"  Recall@0.5: {recall_50:.3f}", flush=True)
    print(f"  mAP@0.5:0.95: {map_50_95:.3f}", flush=True)
    print(f"  Total time: {elapsed_time:.2f}s", flush=True)
    print(f"Results saved to {output_path}", flush=True)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Object Detection Validator with COCO")
    parser.add_argument('--config', type=str, required=True, help='Path to the validation configuration JSON file.')
    args = parser.parse_args()
    main(args.config)
