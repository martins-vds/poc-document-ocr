# Review Page UX Enhancements

This document describes the user experience improvements made to the Review page for better document validation.

## Overview

The Review page has been enhanced to provide reviewers with more context about the quality of extracted data, making it easier to identify fields that need attention and verify the accuracy of OCR results.

## Key Features

### 1. Confidence Level Display

Each extracted field now displays its confidence level as a percentage badge next to the field label. The confidence level is provided by Azure Document Intelligence and indicates how confident the OCR engine is about the accuracy of the extracted value.

**Visual Indicators:**
- **90%+ confidence**: Green badge (`bg-success`) - High confidence
- **70-89% confidence**: Blue badge (`bg-info`) - Medium-high confidence
- **50-69% confidence**: Yellow badge (`bg-warning`) - Medium confidence
- **Below 50% confidence**: Red badge (`bg-danger`) - Low confidence

### 2. Field Border Color Coding

Input fields are color-coded based on their confidence level, providing an at-a-glance visual indicator:
- **Green border**: High confidence (90%+)
- **Blue border**: Medium-high confidence (70-89%)
- **Yellow border**: Medium confidence (50-69%)
- **Red border**: Low confidence (<50%)

### 3. Low Confidence Warnings

Fields with confidence levels below 70% display a warning message below the input field:
> ⚠️ Low confidence - please verify carefully

This ensures reviewers pay extra attention to fields that may contain errors.

### 4. Field Type Display

Each field shows its data type (e.g., String, Date, PhoneNumber, Double) in a subtle badge on the right side of the input field. This helps reviewers understand what type of data is expected and validate it accordingly.

### 5. Improved Form Layout

The form layout has been enhanced with:
- **Bold field labels**: Makes field names more prominent
- **Placeholder text**: Guides reviewers with "Enter or correct the value"
- **Help text**: A subtitle explains the purpose of confidence levels
- **Better spacing**: Improved visual hierarchy and readability

## User Interface Details

### Field Structure

Each field in the review form now displays:

```
┌─────────────────────────────────────────────────────────────┐
│ FieldName                            [95.2% confidence]      │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Extracted value here                  │ [Field Type]    │ │
│ └─────────────────────────────────────────────────────────┘ │
│ ⚠️ Low confidence - please verify carefully (if < 70%)      │
└─────────────────────────────────────────────────────────────┘
```

### Color Scheme

The confidence-based color scheme follows Bootstrap's standard colors:
- **Green** (Success): Highly accurate, minimal review needed
- **Blue** (Info): Good accuracy, standard verification recommended
- **Yellow** (Warning): Moderate accuracy, careful review needed
- **Red** (Danger): Low accuracy, thorough verification required

## Technical Implementation

### New Helper Methods

The following methods were added to the Review page component:

1. **`GetFieldConfidence(object value)`**: Extracts confidence level from field data
2. **`GetFieldType(object value)`**: Retrieves the field type from field data
3. **`GetConfidenceBadgeClass(float? confidence)`**: Determines badge color class based on confidence
4. **`GetFieldBorderClass(float? confidence)`**: Determines input border color class based on confidence

### Data Structure

The extracted data from Azure Document Intelligence is stored in the following structure:

```json
{
  "FieldName": {
    "type": "String",
    "confidence": 0.952,
    "content": "Extracted text value",
    "valueString": "Extracted text value"
  }
}
```

## Benefits for Reviewers

1. **Prioritization**: Quickly identify fields that need the most attention
2. **Confidence**: Visual feedback about data quality increases trust in the system
3. **Efficiency**: Spend more time on uncertain fields, less on high-confidence ones
4. **Context**: Understanding field types helps validate data appropriately
5. **Better UX**: Clear visual hierarchy and intuitive design reduces cognitive load

## Dependencies

- **Bootstrap 5.3.0**: For styling and layout
- **Bootstrap Icons 1.11.0**: For confidence and warning icons
- **Blazor Server**: For interactive server-side rendering

## Browser Compatibility

The enhanced UI uses standard Bootstrap components and should work on all modern browsers:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)

## Future Enhancements

Potential improvements for future iterations:
1. Sortable fields by confidence level
2. Filter to show only low-confidence fields
3. Bulk edit mode for similar fields
4. Confidence history tracking
5. AI-assisted suggestions for corrections
6. Keyboard shortcuts for faster navigation
