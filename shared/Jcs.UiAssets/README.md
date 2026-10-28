# Jcs.UiAssets

다른 데스크톱 프로그램에서도 같이 쓰는 UI 자산입니다. NuGet 패키지 `Jcs.UiAssets`로 넣거나, `content/jcs-ui` 폴더를 그대로 복사합니다. 실행 중 CDN은 쓰지 않습니다.

## 들어 있는 것

| 경로 | 내용 |
|---|---|
| `content/jcs-ui/jcs-glass.css` | iOS 26식 유리 표면, 세그먼트, 카드, 버튼 |
| `content/jcs-ui/icons/*.svg` | Lucide 0.544.0. 라이선스는 ISC. `LICENSE-LUCIDE.txt` |
| `content/jcs-ui/brand/mark.png` | 원본 M-Link 마크 64px |
| `content/jcs-ui/brand/mark-180.png` | 같은 마크 180px |
| `content/jcs-ui/brand/mark.ico` | 같은 마크 아이콘 |

## 다른 저장소에서

오프라인 피드에 패키지가 있으면:

```
nuget install Jcs.UiAssets -Source C:\OfflineDev\nuget-packages -OutputDirectory packages
```

피드에 없을 때는 이 폴더에서 만듭니다.

```
nuget pack Jcs.UiAssets.nuspec -OutputDirectory ..\..\offline-kit\nuget-packages
```

HTML에서는 `jcs-glass.css`를 읽고, 아이콘은 CSS `mask`로 칠합니다. `currentColor`라서 버튼 색을 따라갑니다.
