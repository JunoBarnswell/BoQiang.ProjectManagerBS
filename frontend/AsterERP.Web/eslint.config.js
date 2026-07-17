import js from '@eslint/js';
import tsParser from '@typescript-eslint/parser';
import tsPlugin from '@typescript-eslint/eslint-plugin';
import reactHooks from 'eslint-plugin-react-hooks';
import importPlugin from 'eslint-plugin-import';

/** @type {import('eslint').Linter.FlatConfig[]} */
export default [
  {
    ignores: [
      'dist/**',
      'node_modules/**',
      'coverage/**',
      'public/vendor/**',
      'public/wasm/**',
      'package-lock.json',
      'src/**/*.d.ts'
    ]
  },
  js.configs.recommended,
  {
    files: ['*.config.{js,cjs,mjs,ts}', 'scripts/**/*.{js,cjs,mjs}'],
    languageOptions: {
      globals: {
        __dirname: 'readonly',
        console: 'readonly',
        process: 'readonly'
      }
    }
  },
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        sourceType: 'module',
        ecmaVersion: 2024,
        ecmaFeatures: {
          jsx: true
        }
      },
      globals: {
        window: 'readonly',
        document: 'readonly',
        navigator: 'readonly'
      }
    },
    plugins: {
      '@typescript-eslint': tsPlugin,
      'react-hooks': reactHooks,
      import: importPlugin
    },
    rules: {
      ...tsPlugin.configs.recommended.rules,
      ...reactHooks.configs.recommended.rules,
      'no-undef': 'off',
      'import/order': [
        'error',
        {
          alphabetize: {
            order: 'asc',
            caseInsensitive: true
          },
          'newlines-between': 'always',
          groups: ['builtin', 'external', 'internal', 'parent', 'sibling', 'index'],
          pathGroups: [
            {
              group: 'internal',
              pattern: '@/**',
              position: 'after'
            }
          ],
          pathGroupsExcludedImportTypes: ['react']
        }
      ],
      'import/no-duplicates': 'error',
      'no-console': ['error', { allow: ['warn', 'error'] }],
      'react-hooks/exhaustive-deps': 'error',
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@mui/icons-material',
              message: 'Please use deep imports for MUI icons (e.g., import AddIcon from "@mui/icons-material/Add") to avoid massive memory usage during Vite build.'
            }
          ]
        }
      ]
    }
  }
];
