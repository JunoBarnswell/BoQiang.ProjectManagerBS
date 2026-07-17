import { isAbsolute, relative, resolve, sep } from 'node:path';

import { fileViewerRenderers } from '@file-viewer/vite-plugin';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react-swc';
import { visualizer } from 'rollup-plugin-visualizer';
import { defineConfig } from 'vite';

export default defineConfig(({ mode }) => {
  const isAnalyzeMode = mode === 'analyze';
  const appBasePath = normalizeBasePath(process.env.VITE_APP_BASE_PATH);
  const appOutDir = resolveFrontendOutputDir(process.env.VITE_APP_OUT_DIR);
  const targetAppCode = normalizeOptionalString(process.env.VITE_APP_TARGET_APP_CODE);
  const normalizedTargetAppCode = targetAppCode?.toUpperCase() ?? '';
  const isTargetApplicationBuild = normalizedTargetAppCode.length > 0 && normalizedTargetAppCode !== 'SYSTEM';
  const workspaceRoutesModule = isTargetApplicationBuild
    ? './src/app/router/workspaceRoutes.target.tsx'
    : './src/app/router/workspaceRoutes.full.tsx';
  const runtimeRegistryModule = resolveRuntimeRegistryModule(normalizedTargetAppCode);
  const dashboardModule = isTargetApplicationBuild
    ? './src/pages/dashboard/DashboardPage.target.tsx'
    : './src/pages/dashboard/DashboardPage.tsx';
  const navigationRoutesModule = isTargetApplicationBuild
    ? './src/app/navigation/routes.target.ts'
    : './src/app/navigation/routes.ts';
  const i18nMessagesModule = isTargetApplicationBuild
    ? './src/core/i18n/messages.target.ts'
    : './src/core/i18n/messages.ts';

  return {
    base: appBasePath,
    define: {
      __APPSHELL_BUILD_TIME__: JSON.stringify(new Date().toISOString()),
      // Vite 8's dev client contains these placeholders. Defining them here keeps
      // the client executable when the dev middleware does not rewrite its own
      // internal client module (for example in the embedded browser).
      __BUNDLED_DEV__: 'false',
      __SERVER_FORWARD_CONSOLE__: 'false',
      global: 'globalThis',
      'process.env': {}
    },
    plugins: [
      {
        name: 'resolve-browser-fs-promises',
        enforce: 'pre',
        resolveId(source) {
          if (source === 'fs/promises' || source === 'node:fs/promises') {
            return resolve(__dirname, './src/shims/fs-promises.ts');
          }

          return null;
        }
      },
      react(),
      tailwindcss(),
      fileViewerRenderers({
        copyAssets: true,
        autoPresets: false,
        chunkStrategy: 'none'
      })
    ],
    resolve: {
      alias: [
        {
          find: 'buffer',
          replacement: 'buffer'
        },
        {
          find: 'process',
          replacement: 'process'
        },
        {
          find: 'util',
          replacement: 'util'
        },
        {
          find: 'stream',
          replacement: 'stream-browserify'
        },
        {
          find: 'path',
          replacement: 'path-browserify'
        },
        {
          find: 'events',
          replacement: 'events'
        },
        {
          find: 'zlib',
          replacement: 'browserify-zlib'
        },
        {
          find: /^fs\/promises$/,
          replacement: resolve(__dirname, './src/shims/fs-promises.ts')
        },
        {
          find: /^node:fs\/promises$/,
          replacement: resolve(__dirname, './src/shims/fs-promises.ts')
        },
        {
          find: /^fs$/,
          replacement: resolve(__dirname, './node_modules/node-stdlib-browser/esm/mock/empty.js')
        },
        {
          find: /^node:fs$/,
          replacement: resolve(__dirname, './node_modules/node-stdlib-browser/esm/mock/empty.js')
        },
        {
          find: '@/app/router/workspaceRoutes',
          replacement: resolve(__dirname, workspaceRoutesModule)
        },
        {
          find: '@/apps/runtimeRegistry',
          replacement: resolve(__dirname, runtimeRegistryModule)
        },
        {
          find: '@/pages/dashboard/DashboardPage',
          replacement: resolve(__dirname, dashboardModule)
        },
        {
          find: '@/app/navigation/routes',
          replacement: resolve(__dirname, navigationRoutesModule)
        },
        {
          find: '@/core/i18n/messages',
          replacement: resolve(__dirname, i18nMessagesModule)
        },
        {
          find: '@',
          replacement: resolve(__dirname, './src')
        }
      ]
    },
    build: {
      ...(appOutDir ? { outDir: appOutDir } : {}),
      emptyOutDir: true,
      // 关闭 gzip 大小计算可加速构建约 10~20 秒
      reportCompressedSize: false,
      chunkSizeWarningLimit: 5000,
      rollupOptions: {
        plugins: [
          ...(isAnalyzeMode
            ? [
                visualizer({
                  gzipSize: true,
                  open: false,
                  filename: 'dist/analyze.html',
                  template: 'treemap'
                })
              ]
            : [])
        ]
      },
      rolldownOptions: {
        output: {
          // 将巨型 vendor chunk（19 MB）拆分为多个功能域独立包，
          // 大幅降低 Rolldown 构建时的单次内存峰值，从而减少或消除对
          // NODE_OPTIONS=--max-old-space-size=8192 的依赖。
          codeSplitting: {
            groups: [
              {
                name: 'react-vendor',
                test: /node_modules[\\/](react|react-dom|react-router|scheduler)([\\/]|$)/,
                priority: 100
              },
              {
                name: 'monaco-editor',
                test: /node_modules[\\/]monaco-editor[\\/]/,
                priority: 95
              },
              {
                name: 'vendor-bpmn',
                test:
                  /node_modules[\\/](bpmn-js|bpmn-io|bpmn-moddle|diagram-js|ids|moddle|moddle-xml|min-dom|min-dash|object-refs|saxen|tiny-svg)([\\/]|$)/,
                priority: 90
              },
              {
                name: 'vendor-xyflow',
                test: /node_modules[\\/]@xyflow[\\/]/,
                priority: 85
              },
              {
                name: 'vendor-three',
                test: /node_modules[\\/](three|three-mesh-bvh)([\\/]|$)/,
                priority: 80
              },
              {
                name: 'vendor-mui-icons',
                test: /node_modules[\\/]@mui[\\/]icons-material[\\/]/,
                priority: 75
              },
              {
                name: 'vendor-mui',
                test: /node_modules[\\/](@mui|@emotion)[\\/]/,
                priority: 70
              },
              {
                name: 'vendor-icons',
                test: /node_modules[\\/](lucide-react|@tabler[\\/]icons-react)([\\/]|$)/,
                priority: 65
              },
              {
                name: 'vendor-tanstack',
                test: /node_modules[\\/]@tanstack[\\/]/,
                priority: 60
              },
              {
                name: 'vendor-signalr',
                test: /node_modules[\\/]@microsoft[\\/]signalr[\\/]/,
                priority: 55
              },
              {
                name: 'vendor-zustand',
                test: /node_modules[\\/]zustand[\\/]/,
                priority: 50
              },
              {
                name: 'vendor-file-utils',
                test: /node_modules[\\/](avsc|@ljheee)([\\/]|$)/,
                priority: 45
              },
              {
                name: 'vendor-graph-layout',
                test: /node_modules[\\/](elkjs|d3-.*)([\\/]|$)/,
                priority: 40
              },
              {
                name: 'vendor-date',
                test: /node_modules[\\/](dayjs|date-fns)([\\/]|$)/,
                priority: 35
              },
              {
                name: 'vendor-node-polyfills',
                test:
                  /node_modules[\\/](crypto-browserify|stream-browserify|buffer|browserify-zlib|events|util|path-browserify|node-stdlib-browser)([\\/]|$)/,
                priority: 30
              }
            ]
          }
        }
      },
      sourcemap: isAnalyzeMode
    },
    optimizeDeps: {
      include: ['react', 'react-dom', 'react-router-dom', 'lucide-react', '@tanstack/react-virtual', 'zustand'],
      // monaco-editor 已改为动态 import()，不需要预优化，排除可减少 dev 启动内存
      exclude: ['monaco-editor']
    },
    server: {
      host: '127.0.0.1',
      port: 5173,
      hmr: {
        overlay: true
      },
      proxy: {
        '/api': {
          target: 'http://127.0.0.1:5000',
          changeOrigin: true
        },
        '/hubs': {
          target: 'http://127.0.0.1:5000',
          changeOrigin: true,
          ws: true
        },
        '/uploads': {
          target: 'http://127.0.0.1:5000',
          changeOrigin: true
        }
      }
    }
  };
});

function normalizeBasePath(value: string | undefined): string {
  const trimmed = value?.trim();
  if (!trimmed || trimmed === '/') {
    return '/';
  }

  const withLeadingSlash = trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
  return withLeadingSlash.endsWith('/') ? withLeadingSlash : `${withLeadingSlash}/`;
}

function normalizeOptionalString(value: string | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function resolveFrontendOutputDir(value: string | undefined): string | null {
  const configuredOutputDir = normalizeOptionalString(value);
  if (!configuredOutputDir) {
    return null;
  }

  const outputDir = resolve(__dirname, configuredOutputDir);
  const relativeOutputDir = relative(__dirname, outputDir);
  if (isAbsolute(relativeOutputDir) || relativeOutputDir === '..' || relativeOutputDir.startsWith(`..${sep}`)) {
    throw new Error('VITE_APP_OUT_DIR must stay inside frontend/AsterERP.Web; publish through publish:frontend.');
  }

  return outputDir;
}

function resolveRuntimeRegistryModule(targetAppCode: string): string {
  if (!targetAppCode || targetAppCode === 'SYSTEM') {
    return './src/apps/runtimeRegistry.full.ts';
  }

  if (targetAppCode === 'WMS') {
    return './src/apps/runtimeRegistry.wms.ts';
  }

  if (targetAppCode === 'MES') {
    return './src/apps/runtimeRegistry.mes.ts';
  }

  return './src/apps/runtimeRegistry.empty.ts';
}
