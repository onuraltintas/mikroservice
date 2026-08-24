/**
 * Engine Factory
 * engineType'a göre uygun engine instance'ı oluşturur.
 */

import { BaseEngine } from './base-engine.interface';
import { GridInteractionEngine } from './grid-interaction.engine';
import { MotionPathEngine } from './motion-path.engine';
import { TextStreamEngine } from './text-stream.engine';
import { TextFadeEngine } from './text-fade.engine';
import { WordHighlightEngine } from './word-highlight.engine';
import { VisualExpansionEngine } from './visual-expansion.engine';
import { ScanFindEngine } from './scan-find.engine';
import { ReadingComprehensionEngine } from './reading-comprehension.engine';
import { RegressionReductionEngine } from './regression-reduction.engine';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';
import { VisualizationEngine } from './visualization.engine';
import { FocusEngine } from './focus.engine';
import { VocabularyBuilderEngine } from './vocabulary-builder.engine';
import { ErrorAnalysisEngine } from './error-analysis.engine';

import { ExamSimulationEngine } from './exam-simulation.engine';

export type EngineType =
    | 'grid_interaction'            // Schulte Table
    | 'text_stream'                 // Tachistoscope, RSVP
    | 'motion_path'                 // Eye Tracking, Fixation, Saccade
    | 'text_fade'                   // Text Fading
    | 'word_highlight'              // Speed Reading, Chunking
    | 'visual_expansion'            // Visual Expansion
    | 'word_group'                  // Deprecated alias
    | 'scan_find'                   // Scanning
    | 'reading_comprehension'       // Comprehension (Anlama Testi)
    | 'exam_simulation'             // Exam Simulation (Sınav Simülasyonu)
    | 'free_reading'                // Free Reading (Serbest Okuma)
    | 'regression_reduction'        // Regression Reduction
    | 'subvocalization_reduction'   // Subvocalization Reduction (İç Ses Azaltma)
    | 'visualization'               // Visualization Training
    | 'attention_training'          // Focus/Attention Training (Odaklanma)
    | 'focus'                       // Focus Training alias
    | 'vocabulary_builder'          // Vocabulary Builder (Kelime Hazinesi)
    | 'error_analysis'              // Error Analysis (Hata Analizi / Proofreading)
    | 'scanning'                   // Alias for scan_find
    | 'skimming';                  // Alias for scan_find (Skimming)

// Engine constructor type
type EngineConstructor = new () => BaseEngine;

// Registry of supported engines
const engineRegistry: Partial<Record<EngineType, EngineConstructor>> = {
    'grid_interaction': GridInteractionEngine,
    'motion_path': MotionPathEngine,
    'text_stream': TextStreamEngine,
    'text_fade': TextFadeEngine,
    'word_highlight': WordHighlightEngine,
    'visual_expansion': VisualExpansionEngine,
    'scan_find': ScanFindEngine,
    'reading_comprehension': ReadingComprehensionEngine,
    'exam_simulation': ExamSimulationEngine,
    'free_reading': ReadingComprehensionEngine,  // Uses same engine as comprehension (self-paced reading + questions)
    'regression_reduction': RegressionReductionEngine,
    'subvocalization_reduction': SubvocalizationReductionEngine,
    'visualization': VisualizationEngine,
    'attention_training': FocusEngine,
    'focus': FocusEngine,
    'vocabulary_builder': VocabularyBuilderEngine,
    'error_analysis': ErrorAnalysisEngine,
    'scanning': ScanFindEngine,
    'skimming': ScanFindEngine, // Fix for Skimming exercise type alias
};

export class EngineFactory {
    static create(engineType: EngineType): BaseEngine | null {
        const EngineClass = engineRegistry[engineType];

        if (!EngineClass) {
            console.warn(`[EngineFactory] Unknown engine type: ${engineType}`);
            return null;
        }

        return new EngineClass();
    }

    static isSupported(engineType: string): boolean {
        return engineType in engineRegistry;
    }

    static getSupportedEngines(): EngineType[] {
        return Object.keys(engineRegistry) as EngineType[];
    }
}
